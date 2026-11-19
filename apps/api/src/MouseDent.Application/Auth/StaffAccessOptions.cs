namespace MouseDent.Application.Auth;

/// <summary>
/// Configuración de los enlaces de acceso. La URL pública se inyecta con
/// <c>StaffAccess__PublicBaseUrl</c> y no se guarda en el repositorio.
/// </summary>
public sealed class StaffAccessOptions
{
    public const string SectionName = "StaffAccess";

    /// <summary>URL pública de la web, sin barra final. Obligatoria para enviar enlaces.</summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>Ruta pública de la pantalla que consume el enlace.</summary>
    public string ResetPath { get; init; } = "/establecer-contrasena";
}
