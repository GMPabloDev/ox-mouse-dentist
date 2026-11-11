using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MouseDent.Infrastructure.Locales;

/// <summary>
/// Prepara la carpeta de fotos al arrancar para que un despliegue sin ese
/// almacenamiento falle al iniciar y no en la primera subida.
/// </summary>
public sealed class LocalPhotoStorageInitializationService(
    FileSystemLocalPhotos photos,
    ILogger<LocalPhotoStorageInitializationService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        photos.EnsureCreated();
        logger.LogInformation("Las fotos de los locales se guardan en {PhotosPath}", photos.RootPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
