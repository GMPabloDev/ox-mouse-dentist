namespace MouseDent.Application.Auth;

/// <summary>
/// Puerto de envío de correo. La implementación SMTP vive en Infrastructure y
/// traduce los fallos del servidor a <see cref="EmailDeliveryException"/>, de
/// modo que el caso de uso no conozca el transporte.
/// </summary>
public interface IEmailSender
{
    /// <summary>Entrega el mensaje al destinatario.</summary>
    /// <exception cref="EmailDeliveryException">El correo no se pudo entregar.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Correo de texto plano que compone la aplicación.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Fallo de entrega. Conserva el motivo para el registro del adaptador y no
/// expone credenciales ni el contenido del enlace.
/// </summary>
public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
