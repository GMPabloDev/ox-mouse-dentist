namespace MouseDent.Application.Locales;

/// <summary>
/// Puerto del almacenamiento persistente de fotos. El guardado es en dos pasos:
/// el archivo se escribe completo en un pendiente y se publica antes de confirmar la
/// escritura del local, de modo que un guardado fallido no deja una
/// foto visible ni toca la anterior.
/// </summary>
public interface ILocalPhotos
{
    /// <summary>Escribe el contenido en un pendiente y devuelve el nombre definitivo.</summary>
    Task<StagedLocalPhoto> StageAsync(Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Publica el pendiente con el nombre definitivo. Propaga los fallos de E/S.</summary>
    void Commit(StagedLocalPhoto staged);

    /// <summary>Descarta el pendiente o el archivo publicado. No propaga fallos de E/S.</summary>
    Task DiscardAsync(StagedLocalPhoto staged, CancellationToken cancellationToken);

    /// <summary>Devuelve los bytes de la foto o <c>null</c> si el archivo no existe.</summary>
    Task<LocalPhotoContent?> OpenAsync(string fileName, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Borra la foto indicada. Devuelve <c>false</c> si no pudo hacerlo; el
    /// adaptador lo registra y el fallo no revierte un guardado ya confirmado.
    /// </summary>
    Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken);
}

/// <summary>
/// Foto escrita en un pendiente. <paramref name="PendingLocation"/> es opaco
/// para Application: solo el adaptador sabe cómo publicarla o descartarla.
/// </summary>
public sealed record StagedLocalPhoto(string FileName, string ContentType, string PendingLocation)
{
    public bool Published { get; set; }
}

/// <summary>Contenido de una foto con el tipo con el que se sirve.</summary>
public sealed record LocalPhotoContent(string ContentType, byte[] Content);
