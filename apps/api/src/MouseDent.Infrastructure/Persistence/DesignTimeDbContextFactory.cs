using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Permite ejecutar <c>dotnet ef</c> sin arrancar el host ni exigir configuración de JWT.
/// La cadena de conexión se lee de appsettings.json del proyecto API.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MouseDentDbContext>
{
    public MouseDentDbContext CreateDbContext(string[] args)
    {
        var projectDirectory = FindApiProjectDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=mousedent.db";
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(projectDirectory, builder.DataSource));
        }

        var options = new DbContextOptionsBuilder<MouseDentDbContext>()
            .UseSqlite(builder.ToString())
            .Options;

        return new MouseDentDbContext(options);
    }

    private static string FindApiProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MouseDent.Api.csproj")))
            {
                return directory.FullName;
            }

            var apiProject = Path.Combine(directory.FullName, "src", "MouseDent.Api", "MouseDent.Api.csproj");
            if (File.Exists(apiProject))
            {
                return Path.GetDirectoryName(apiProject)!;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró el proyecto MouseDent.Api para leer appsettings.json.");
    }
}
