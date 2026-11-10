using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Api.Tests;

/// <summary>
/// Arranca la API en memoria con una base SQLite propia por instancia.
/// Reutiliza el sembrado de Development para probar el acceso del personal.
/// </summary>
public sealed class MouseDentApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public MouseDentApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:AdminEmail"] = "admin@mousedent.local",
                ["Seed:AdminPassword"] = "Admin123!",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MouseDentDbContext>>();
            services.RemoveAll<MouseDentDbContext>();
            services.AddDbContext<MouseDentDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
