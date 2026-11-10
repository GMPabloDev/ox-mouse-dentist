using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones y el sembrado de desarrollo cuando el host arranca.
/// </summary>
public sealed class DatabaseInitializationService(
    IServiceProvider services,
    ILogger<DatabaseInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DatabaseInitializer.MigrateAndSeedAsync(services, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "No se pudo inicializar la base de datos");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
