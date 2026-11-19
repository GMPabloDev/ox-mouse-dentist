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
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Api.Tests;

/// <summary>
/// Arranca la API con una carpeta de fotos propia por instancia. Reutiliza el
/// sembrado de Development para probar el acceso del personal y agrega cuentas
/// con otros perfiles para las pruebas de autorización.
/// </summary>
public abstract class MouseDentApiFactoryBase : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@mousedent.local";
    public const string AdminPassword = "Admin123!";
    public const string ReceptionEmail = "recepcion@mousedent.local";
    public const string ReceptionPassword = "Recepcion123!";

    /// <summary>URL pública de la web con la que se construyen los enlaces del correo.</summary>
    public const string PublicBaseUrl = "https://panel.mousedent.test";

    private readonly SqliteConnection? _connection;
    private readonly string? _databasePath;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _tokens = new();

    /// <param name="useFileDatabase">
    /// Con <c>true</c> la base vive en un archivo temporal y cada solicitud abre su
    /// propia conexión, de modo que se pueden comprobar escrituras concurrentes.
    /// Con <c>false</c> una única conexión en memoria comparte la base con el host.
    /// </param>
    protected MouseDentApiFactoryBase(bool useFileDatabase)
    {
        if (useFileDatabase)
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "mousedent-tests",
                $"{Guid.NewGuid():N}.db");
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            return;
        }

        _connection = new SqliteConnection("Data Source=:memory:");
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
            var values = new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Media:PhotosPath"] = PhotosPath,
                ["StaffAccess:PublicBaseUrl"] = PublicBaseUrl,

                // Las pruebas no compiten por los limitadores: el límite configurado se
                // comprueba en una fábrica propia con un valor bajo.
                ["RateLimits:LoginPermitLimit"] = "1000",
                ["RateLimits:PasswordRecoveryPermitLimit"] = "1000",
            };

            ConfigureTestSettings(values);
            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MouseDentDbContext>>();
            services.RemoveAll<MouseDentDbContext>();

            if (_connection is not null)
            {
                services.AddDbContext<MouseDentDbContext>(options => options.UseSqlite(_connection));
            }
            else
            {
                // Cada contexto abre su propia conexión al archivo temporal, así dos
                // solicitudes concurrentes escriben en conexiones distintas.
                services.AddDbContext<MouseDentDbContext>(options =>
                    options.UseSqlite($"Data Source={_databasePath}"));
            }

            // El correo se registra en memoria: ninguna prueba habla con un servidor real.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<RecordingEmailSender>());

            ConfigureTestServices(services);
        });
    }

    /// <summary>Correos que la API habría enviado en esta instancia.</summary>
    internal RecordingEmailSender Emails => Services.GetRequiredService<RecordingEmailSender>();

    /// <summary>Ajustes de configuración propios de una prueba concreta.</summary>
    protected virtual void ConfigureTestSettings(Dictionary<string, string?> settings)
    {
    }

    /// <summary>Reemplazos de servicios propios de una prueba concreta.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    /// <summary>Crea la cuenta con perfil <c>Recepcion</c> que usan las pruebas de autorización.</summary>
    public async Task SeedReceptionAccountAsync() =>
        await SeedAccountAsync(ReceptionEmail, ReceptionPassword, StaffRole.Reception);

    /// <summary>
    /// Crea una cuenta con contraseña establecida, como si la persona ya hubiera
    /// aceptado su invitación. Devuelve su identificador.
    /// </summary>
    public async Task<Guid> SeedAccountAsync(
        string email,
        string password,
        string profile,
        bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing.Id;
        }

        var account = new StaffUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"{profile} de pruebas",
            Profile = profile,
            IsActive = isActive,
            PasswordEstablishedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
        };

        var created = await users.CreateAsync(account, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta de pruebas: {string.Join("; ", created.Errors.Select(error => error.Description))}");
        }

        return account.Id;
    }

    /// <summary>
    /// Token de acceso por correo. Se pide una sola vez porque el limitador de
    /// acceso admite diez intentos por minuto y minuto en cada instancia.
    /// </summary>
    public Task<string> AccessTokenAsync(string email, string password) =>
        _tokens.GetOrAdd(
            email,
            _ => new Lazy<Task<string>>(() => RequestTokenAsync(email, password))).Value;

    /// <summary>Token nuevo, sin reutilizar el de <see cref="AccessTokenAsync"/>.</summary>
    public Task<string> LoginAsync(string email, string password) => RequestTokenAsync(email, password);

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _connection?.Dispose();

        if (_databasePath is not null)
        {
            SqliteConnection.ClearAllPools();

            foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        if (Directory.Exists(PhotosPath))
        {
            Directory.Delete(PhotosPath, recursive: true);
        }
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
}

/// <summary>API con una base SQLite en memoria compartida por todo el host.</summary>
public sealed class MouseDentApiFactory() : MouseDentApiFactoryBase(useFileDatabase: false);

/// <summary>
/// API con una base SQLite en archivo, para pruebas que necesitan conexiones
/// independientes y escrituras concurrentes.
/// </summary>
public sealed class MouseDentApiFileDatabaseFactory() : MouseDentApiFactoryBase(useFileDatabase: true);

/// <summary>
/// API con el reloj bajo control de la prueba, para comprobar la vigencia de los
/// enlaces sin tocar la base.
/// </summary>
public sealed class MouseDentApiClockFactory() : MouseDentApiFactoryBase(useFileDatabase: false)
{
    internal TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }
}

/// <summary>
/// API con el límite de recuperación bajo (tres solicitudes por ventana) para
/// comprobar el límite configurado sin agotar el de las demás pruebas.
/// </summary>
public sealed class MouseDentApiRecoveryLimitFactory() : MouseDentApiFactoryBase(useFileDatabase: false)
{
    public const int RecoveryPermitLimit = 3;

    protected override void ConfigureTestSettings(Dictionary<string, string?> settings)
    {
        settings["RateLimits:PasswordRecoveryPermitLimit"] = RecoveryPermitLimit.ToString();
        settings["RateLimits:WindowSeconds"] = "600";
    }
}
