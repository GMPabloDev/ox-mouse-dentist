using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MouseDent.Application.Locales;

namespace MouseDent.Infrastructure.Locales;

/// <summary>
/// Guarda las fotos en el sistema de archivos del servidor. El nombre lo genera
/// el servidor; el tipo de contenido se registra con el local. Los pendientes
/// viven en una carpeta aparte y se publican antes de guardar el local.
/// </summary>
public sealed class FileSystemLocalPhotos : ILocalPhotos
{
    private const string PendingFolderName = ".pending";
    private const string PendingSuffix = ".part";

    private readonly ILogger<FileSystemLocalPhotos> _logger;
    private readonly string _pendingPath;

    public FileSystemLocalPhotos(
        IOptions<LocalPhotoOptions> options,
        IHostEnvironment environment,
        ILogger<FileSystemLocalPhotos> logger)
    {
        _logger = logger;
        RootPath = ResolveRootPath(options.Value.PhotosPath, environment);
        _pendingPath = Path.Combine(RootPath, PendingFolderName);
        EnsureCreated();
    }

    public string RootPath { get; }

    /// <summary>Crea las carpetas de fotos y pendientes si no existen.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(_pendingPath);
    }

    public async Task<StagedLocalPhoto> StageAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        return await StageNamedAsync(content, contentType, $"{Guid.NewGuid():N}{ExtensionFor(contentType)}", cancellationToken);
    }

    internal async Task<StagedLocalPhoto> StageNamedAsync(
        Stream content, string contentType, string fileName, CancellationToken cancellationToken)
    {
        var pendingLocation = Path.Combine(_pendingPath, fileName + PendingSuffix);

        var created = false;
        try
        {
            await using var file = new FileStream(
                pendingLocation,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            created = true;

            await content.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            if (created)
            {
                TryDeleteFile(pendingLocation);
            }
            throw;
        }

        return new StagedLocalPhoto(fileName, contentType, pendingLocation);
    }

    public void Commit(StagedLocalPhoto staged)
    {
        var finalPath = Path.Combine(RootPath, staged.FileName);

        File.Move(staged.PendingLocation, finalPath);
        staged.Published = true;
    }

    public Task DiscardAsync(StagedLocalPhoto staged, CancellationToken cancellationToken)
    {
        TryDeleteFile(staged.PendingLocation);
        if (staged.Published)
        {
            TryDeleteFile(Path.Combine(RootPath, staged.FileName));
        }
        return Task.CompletedTask;
    }

    public async Task<LocalPhotoContent?> OpenAsync(
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(RootPath, Path.GetFileName(fileName));
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new LocalPhotoContent(contentType, content);
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(RootPath, Path.GetFileName(fileName));

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "No se pudo borrar la foto anterior {FileName}", fileName);
            return Task.FromResult(false);
        }
    }

    private static string ResolveRootPath(string? configured, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured, environment.ContentRootPath);
        }

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            throw new InvalidOperationException(
                "Configura Media:PhotosPath: la carpeta de datos locales no está disponible en este entorno.");
        }

        return Path.Combine(localData, "MouseDent", "photos");
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin",
    };

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "No se pudo borrar el archivo temporal {Path}", path);
        }
    }
}
