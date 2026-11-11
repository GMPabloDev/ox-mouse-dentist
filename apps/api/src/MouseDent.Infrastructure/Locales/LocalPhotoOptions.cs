namespace MouseDent.Infrastructure.Locales;

/// <summary>
/// Configuración del almacenamiento de fotos. <c>Media:PhotosPath</c> debe
/// apuntar a un almacenamiento persistente fuera del repositorio; si falta, se
/// usa la carpeta de datos locales del usuario.
/// </summary>
public sealed class LocalPhotoOptions
{
    public const string SectionName = "Media";

    public string? PhotosPath { get; set; }
}
