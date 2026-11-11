using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MouseDent.Application.Locales;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Locales;

/// <summary>
/// Implementa <see cref="ILocales"/> con EF Core. Traduce el choque del índice
/// único del nombre normalizado a <see cref="LocalSaveResult.DuplicateName"/>
/// para que la unicidad se garantice también ante escrituras concurrentes.
/// </summary>
public sealed class EfLocales(MouseDentDbContext database) : ILocales
{
    public async Task<IReadOnlyList<Local>> ListAsync(CancellationToken cancellationToken) =>
        await database.Locales
            .AsNoTracking()
            .OrderBy(local => local.NormalizedName)
            .ToListAsync(cancellationToken);

    public Task<Local?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        database.Locales.FirstOrDefaultAsync(local => local.Id == id, cancellationToken);

    public Task<bool> NameIsTakenAsync(
        string normalizedName,
        Guid? excludeLocalId,
        CancellationToken cancellationToken) =>
        excludeLocalId is null
            ? database.Locales.AnyAsync(local => local.NormalizedName == normalizedName, cancellationToken)
            : database.Locales.AnyAsync(
                local => local.NormalizedName == normalizedName && local.Id != excludeLocalId.Value,
                cancellationToken);

    public async Task<LocalSaveResult> SaveAsync(Local local, CancellationToken cancellationToken)
    {
        if (database.Entry(local).State == EntityState.Detached)
        {
            database.Locales.Add(local);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return LocalSaveResult.Saved;
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            return LocalSaveResult.DuplicateName;
        }
    }

    private static bool IsDuplicateName(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && sqlite.Message.Contains("Locales.NormalizedName", StringComparison.Ordinal);
}
