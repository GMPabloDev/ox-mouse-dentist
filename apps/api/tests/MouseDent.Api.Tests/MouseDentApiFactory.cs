using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Api.Tests;

/// <summary>
/// Arranca la API en memoria con una base SQLite y una carpeta de fotos propias
/// por instancia. Reutiliza el sembrado de Development para probar el acceso del
/// personal y agrega una cuenta con otro perfil para las pruebas de autorización.
/// </summary>
public sealed class MouseDentApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@mousedent.local";
    public const string AdminPassword = "Admin123!";
    public const string ReceptionEmail = "recepcion@mousedent.local";
    public const string ReceptionPassword = "Recepcion123!";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _tokens = new();

    public MouseDentApiFactory()
    {
        _connection.Open();
    }

    /// <summary>Carpeta de fotos de esta instancia, fuera del repositorio.</summary>
    public string PhotosPath { get; } = Path.Combine(
        Path.GetTempPath(),
        "mousedent-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Media:PhotosPath"] = PhotosPath,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MouseDentDbContext>>();
            services.RemoveAll<MouseDentDbContext>();
            services.AddDbContext<MouseDentDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Crea la cuenta con perfil <c>Recepcion</c> que usan las pruebas de autorización.</summary>
    public async Task SeedReceptionAccountAsync()
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();

        if (await users.FindByEmailAsync(ReceptionEmail) is not null)
        {
            return;
        }

        var account = new StaffUser
        {
            UserName = ReceptionEmail,
            Email = ReceptionEmail,
            EmailConfirmed = true,
            DisplayName = "Recepción de pruebas",
        };

        var created = await users.CreateAsync(account, ReceptionPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta de pruebas: {string.Join("; ", created.Errors.Select(error => error.Description))}");
        }

        var role = await users.AddToRoleAsync(account, StaffRole.Reception);
        if (!role.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar el perfil de pruebas: {string.Join("; ", role.Errors.Select(error => error.Description))}");
        }
    }

    /// <summary>
    /// Token de acceso por correo. Se pide una sola vez porque el limitador de
    /// acceso admite diez intentos por minuto y minuto en cada instancia.
    /// </summary>
    public Task<string> AccessTokenAsync(string email, string password) =>
        _tokens.GetOrAdd(
            email,
            _ => new Lazy<Task<string>>(() => RequestTokenAsync(email, password))).Value;

    /// <summary>Fotos publicadas en la carpeta de esta instancia.</summary>
    public IReadOnlyList<string> PublishedPhotoFiles() =>
        Directory.Exists(PhotosPath)
            ? [.. Directory.GetFiles(PhotosPath).Select(Path.GetFileName).OfType<string>().Order()]
            : [];

    /// <summary>Fotos que quedaron a medio guardar.</summary>
    public IReadOnlyList<string> PendingPhotoFiles()
    {
        var pending = Path.Combine(PhotosPath, ".pending");

        return Directory.Exists(pending)
            ? [.. Directory.GetFiles(pending).Select(Path.GetFileName).OfType<string>().Order()]
            : [];
    }

    private async Task<string> RequestTokenAsync(string email, string password)
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"No se pudo iniciar sesión como {email}: {(int)response.StatusCode}.");
        }

        var login = await response.Content.ReadFromJsonAsync<TokenPayload>();

        return login?.AccessToken
            ?? throw new InvalidOperationException($"La respuesta de acceso para {email} no trae token.");
    }

    private sealed record TokenPayload(string AccessToken);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();

            if (Directory.Exists(PhotosPath))
            {
                Directory.Delete(PhotosPath, recursive: true);
            }
        }
    }
}
