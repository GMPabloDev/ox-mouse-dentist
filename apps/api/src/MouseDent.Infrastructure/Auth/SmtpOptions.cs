namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Configuración del servidor SMTP de Gmail. Los valores llegan por variables de
/// entorno (<c>Smtp__Host</c>, <c>Smtp__Port</c>, <c>Smtp__UserName</c>,
/// <c>Smtp__Password</c>, <c>Smtp__FromAddress</c>) y nunca se guardan en el
/// repositorio.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Servidor SMTP. Para Gmail, <c>smtp.gmail.com</c>.</summary>
    public string? Host { get; set; }

    /// <summary>Puerto SMTP. Gmail usa 587 con STARTTLS.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Cuenta autenticada. Gmail exige una contraseña de aplicación.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Remitente visible. Gmail exige la misma cuenta autenticada o un alias autorizado.</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "MouseDent";

    /// <summary>Espera máxima del servidor, en segundos.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
