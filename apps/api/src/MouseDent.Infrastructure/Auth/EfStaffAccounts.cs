using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Implementa <see cref="IStaffAccounts"/> con UserManager de Identity para las
/// credenciales y con EF Core para el perfil, el estado y las asignaciones.
/// La comprobación del último administrador y su escritura comparten una
/// transacción de escritura, así que dos solicitudes concurrentes se serializan
/// y no pueden dejar el sistema sin administración.
/// </summary>
public sealed class EfStaffAccounts(UserManager<StaffUser> users, MouseDentDbContext database)
    : IStaffAccounts
{
    public async Task<StaffAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        var user = await database.Users.AsNoTracking()
            .Include(stored => stored.Assignments)
            .SingleOrDefaultAsync(stored => stored.NormalizedEmail == normalizedEmail, cancellationToken);

        // La contraseña y la versión del token provienen de la misma lectura.
        if (user is null || !user.IsActive || user.PasswordEstablishedAt is null || user.PasswordHash is null)
        {
            return null;
        }

        var account = ToAccount(user);
        var passwordHash = user.PasswordHash;
        var verified = users.PasswordHasher.VerifyHashedPassword(user, passwordHash, password);
        if (verified == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var upgradedHash = users.PasswordHasher.HashPassword(user, password);
            // No se debe sobrescribir una contraseña restablecida después de la lectura.
            await database.Users
                .Where(stored => stored.Id == user.Id
                    && stored.PasswordHash == passwordHash
                    && stored.SessionVersion == account.SessionVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(stored => stored.PasswordHash, upgradedHash), cancellationToken);
        }

        return account;
    }

    public async Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .Include(stored => stored.Assignments)
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

        return user is null ? null : ToAccount(user);
    }

    public async Task<StaffAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        var user = await database.Users.AsNoTracking()
            .Include(stored => stored.Assignments)
            .SingleOrDefaultAsync(stored => stored.NormalizedEmail == normalizedEmail, cancellationToken);

        return user is null ? null : ToAccount(user);
    }

    public async Task<IReadOnlyList<StaffAccount>> ListAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = database.Users
            .AsNoTracking()
            .Include(user => user.Assignments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = SearchPattern(search.Trim());
            query = query.Where(user =>
                EF.Functions.Like(user.DisplayName, pattern, "\\")
                || EF.Functions.Like(user.Email!, pattern, "\\"));
        }

        var stored = await query
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);

        return [.. stored.Select(ToAccount)];
    }

    public async Task<StaffSaveResult> CreateAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (!await NewAssignmentsAreActiveAsync(account.AssignedLocalIds, [], cancellationToken))
        {
            return StaffSaveResult.LocalsUnavailable;
        }

        var user = new StaffUser
        {
            Id = account.Id,

            // El correo identifica la cuenta y se valida en el borde. El nombre de
            // usuario solo es el identificador interno de Identity: usar el correo
            // rechazaría direcciones válidas con tildes o eñes.
            UserName = account.Id.ToString(),
            Email = account.Email,
            EmailConfirmed = true,
            DisplayName = account.DisplayName,
            PhoneNumber = account.Phone,
            Profile = account.Profile,
            IsActive = account.IsActive,
            PasswordEstablishedAt = account.PasswordEstablishedAt,
            SessionVersion = account.SessionVersion,
        };

        IdentityResult created;
        try
        {
            // Sin contraseña: la cuenta queda pendiente hasta que la persona la establezca.
            created = await users.CreateAsync(user);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return StaffSaveResult.DuplicateEmail;
        }

        if (!created.Succeeded)
        {
            return Failure(created);
        }

        await ReplaceAssignmentsAsync(user.Id, account.AssignedLocalIds, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StaffSaveResult.Saved;
    }

    public async Task<StaffSaveResult> UpdateAsync(StaffAccount account, StaffAccount expected, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var user = await database.Users.Include(stored => stored.Assignments).SingleOrDefaultAsync(
            stored => stored.Id == account.Id,
            cancellationToken);

        if (user is null)
        {
            return StaffSaveResult.NotFound;
        }

        if (user.Email != expected.Email || user.DisplayName != expected.DisplayName
            || user.PhoneNumber != expected.Phone || user.Profile != expected.Profile
            || user.IsActive != expected.IsActive || user.PasswordEstablishedAt != expected.PasswordEstablishedAt
            || user.SessionVersion != expected.SessionVersion
            || !user.Assignments.Select(assignment => assignment.LocalId).ToHashSet().SetEquals(expected.AssignedLocalIds))
        {
            return StaffSaveResult.Changed;
        }

        if (!await NewAssignmentsAreActiveAsync(account.AssignedLocalIds,
            user.Assignments.Select(assignment => assignment.LocalId), cancellationToken))
        {
            return StaffSaveResult.LocalsUnavailable;
        }

        var administersBefore = Administers(user.Profile, user.IsActive, user.PasswordEstablishedAt);
        var administersAfter = Administers(account.Profile, account.IsActive, account.PasswordEstablishedAt);
        if (administersBefore && !administersAfter && !await AnotherAdministratorExistsAsync(user.Id, cancellationToken))
        {
            return StaffSaveResult.LastAdministrator;
        }

        var invalidateLinks = !account.IsActive || !string.Equals(user.Email, account.Email, StringComparison.OrdinalIgnoreCase);
        if (invalidateLinks)
        {
            await database.Set<StaffAccessLink>()
                .Where(link => link.AccountId == account.Id && link.UsedAt == null)
                .ExecuteDeleteAsync(cancellationToken);
        }

        user.Email = account.Email;
        user.DisplayName = account.DisplayName;
        user.PhoneNumber = account.Phone;
        user.Profile = account.Profile;
        user.IsActive = account.IsActive;
        user.PasswordEstablishedAt = account.PasswordEstablishedAt;
        user.SessionVersion = account.SessionVersion;

        await ReplaceAssignmentsAsync(user.Id, account.AssignedLocalIds, cancellationToken);

        IdentityResult updated;
        try
        {
            updated = await users.UpdateAsync(user);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return StaffSaveResult.DuplicateEmail;
        }

        if (!updated.Succeeded)
        {
            return Failure(updated);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StaffSaveResult.Saved;
    }

    private async Task<bool> NewAssignmentsAreActiveAsync(
        IReadOnlyList<Guid> requested, IEnumerable<Guid> existing, CancellationToken cancellationToken)
    {
        var added = requested.Except(existing).ToArray();
        return added.Length == await database.Locales.AsNoTracking()
            .CountAsync(local => added.Contains(local.Id) && local.IsActive, cancellationToken);
    }

    /// <summary>Administración activa que ya estableció su contraseña, sin contar la cuenta indicada.</summary>
    private Task<bool> AnotherAdministratorExistsAsync(Guid excludedId, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(
            user => user.Id != excludedId
                && user.Profile == StaffRole.Admin
                && user.IsActive
                && user.PasswordEstablishedAt != null,
            cancellationToken);

    private static bool Administers(string profile, bool isActive, DateTime? passwordEstablishedAt) =>
        profile == StaffRole.Admin && isActive && passwordEstablishedAt is not null;

    private async Task ReplaceAssignmentsAsync(
        Guid userId,
        IReadOnlyList<Guid> localIds,
        CancellationToken cancellationToken)
    {
        var stored = await database.Set<StaffLocalAssignment>()
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync(cancellationToken);

        var wanted = localIds.ToHashSet();

        database.RemoveRange(stored.Where(assignment => !wanted.Contains(assignment.LocalId)));
        database.AddRange(wanted
            .Where(localId => stored.All(assignment => assignment.LocalId != localId))
            .Select(localId => new StaffLocalAssignment { UserId = userId, LocalId = localId }));
    }

    private static StaffAccount ToAccount(StaffUser user) =>
        StaffAccount.Restore(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.PhoneNumber,
            user.Profile,
            user.IsActive,
            user.PasswordEstablishedAt,
            user.SessionVersion,
            user.Assignments.Select(assignment => assignment.LocalId));

    private static StaffSaveResult Failure(IdentityResult result) =>
        result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
            ? StaffSaveResult.DuplicateEmail
            : throw new InvalidOperationException(
                $"No se pudo guardar la cuenta: {string.Join("; ", result.Errors.Select(error => error.Description))}");

    /// <summary>Solo el correo y el nombre de usuario tienen índices únicos en este modelo.</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };

    private static string SearchPattern(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
