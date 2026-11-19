using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones y, si <c>Seed:Enabled</c> es true, crea los roles y la cuenta
/// de administración de desarrollo.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var database = provider.GetRequiredService<MouseDentDbContext>();
        await database.Database.MigrateAsync(cancellationToken);

        var configuration = provider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("Seed:Enabled", false))
        {
            return;
        }

        await SeedIdentityAsync(provider, configuration, cancellationToken);
    }

    private static async Task SeedIdentityAsync(
        IServiceProvider provider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in StaffRole.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roles.RoleExistsAsync(role))
            {
                continue;
            }

            var created = await roles.CreateAsync(new IdentityRole<Guid>(role));
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el rol {role}: {string.Join("; ", created.Errors.Select(error => error.Description))}");
            }
        }

        var users = provider.GetRequiredService<UserManager<StaffUser>>();
        var clock = provider.GetRequiredService<TimeProvider>();
        var email = configuration["Seed:AdminEmail"] ?? "admin@mousedent.local";
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new StaffUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = configuration["Seed:AdminDisplayName"] ?? "Administración MouseDent",

            // La cuenta semilla ya tiene contraseña: cuenta como administración disponible.
            Profile = StaffRole.Admin,
            IsActive = true,
            PasswordEstablishedAt = clock.GetUtcNow().UtcDateTime,
        };

        var password = configuration["Seed:AdminPassword"] ?? "Admin123!";
        var result = await users.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta semilla: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        // El perfil es la fuente del acceso; el rol se mantiene para conservar el
        // esquema y los datos de roles de SPEC 01.
        await users.AddToRoleAsync(admin, StaffRole.Admin);
    }
}
