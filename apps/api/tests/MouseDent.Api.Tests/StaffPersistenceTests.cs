using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Garantías de la cuenta en la base: el correo normalizado es único en el esquema,
/// así que dos altas simultáneas no pueden dejar dos cuentas, y una escritura que
/// choca con otra restricción no se presenta como un correo ocupado.
/// </summary>
public sealed class StaffPersistenceTests(MouseDentApiFileDatabaseFactory factory)
    : IClassFixture<MouseDentApiFileDatabaseFactory>
{
    private const string Password = "Recepcion123!";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_schema_rejects_two_accounts_with_the_same_normalized_email()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var email = StaffTestHelpers.UniqueEmail("esquema.correo");

        database.Users.Add(NewAccount(email, email.ToUpperInvariant()));
        await database.SaveChangesAsync(Token);

        // El nombre de usuario es distinto: el único índice que puede rechazar la
        // segunda cuenta es el del correo normalizado.
        database.Users.Add(NewAccount(
            StaffTestHelpers.UniqueEmail("esquema.otro"),
            email.ToUpperInvariant()));

        await Should.ThrowAsync<DbUpdateException>(async () => await database.SaveChangesAsync(Token));
        database.ChangeTracker.Clear();

        (await database.Users.CountAsync(
            user => user.NormalizedEmail == email.ToUpperInvariant(),
            Token)).ShouldBe(1);
    }

    [Fact]
    public async Task A_write_rejected_by_another_constraint_is_not_reported_as_a_duplicate_email()
    {
        var existing = await factory.SeedAccountAsync(
            StaffTestHelpers.UniqueEmail("restriccion.cuenta"),
            Password,
            StaffRole.Reception);
        var repeatedId = StaffAccount.Restore(
            existing,
            StaffTestHelpers.UniqueEmail("restriccion.nueva"),
            "Cuenta con identificador repetido",
            phone: null,
            StaffRole.Reception,
            isActive: true,
            passwordEstablishedAt: null,
            sessionVersion: 1,
            assignedLocalIds: []);

        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccounts>();

        // La clave primaria repetida es un defecto de la escritura, no un correo ocupado.
        await Should.ThrowAsync<DbUpdateException>(
            async () => await accounts.CreateAsync(repeatedId, Token));
    }

    private static StaffUser NewAccount(string email, string normalizedEmail) => new()
    {
        Id = Guid.NewGuid(),
        UserName = Guid.NewGuid().ToString(),
        NormalizedUserName = Guid.NewGuid().ToString().ToUpperInvariant(),
        Email = email,
        NormalizedEmail = normalizedEmail,
        EmailConfirmed = true,
        DisplayName = "Cuenta de pruebas",
        Profile = StaffRole.Reception,
        SecurityStamp = Guid.NewGuid().ToString(),
    };
}
