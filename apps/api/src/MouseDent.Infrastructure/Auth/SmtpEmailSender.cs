using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MouseDent.Application.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Envía los correos de acceso con <see cref="SmtpClient"/> de la biblioteca base
/// sobre SMTP autenticado y STARTTLS, el transporte de Gmail. El volumen del MVP
/// no justifica otro paquete. Los fallos se registran con el motivo y se traducen
/// a <see cref="EmailDeliveryException"/>; ni el registro ni la respuesta incluyen
/// credenciales ni el enlace.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;

        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.FromAddress)
            || smtp.Port is < 1 or > 65535 || smtp.TimeoutSeconds is < 1 or > int.MaxValue / 1000)
        {
            logger.LogError("El correo no se envió: la configuración SMTP es inválida o está incompleta.");
            throw new EmailDeliveryException("La configuración SMTP está incompleta.");
        }

        try
        {
            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtp.UserName, smtp.Password),
                Timeout = (int)TimeSpan.FromSeconds(smtp.TimeoutSeconds).TotalMilliseconds,
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false,
            };
            mail.To.Add(message.To);

            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or IOException or FormatException or ArgumentException)
        {
            logger.LogWarning(exception, "No se pudo enviar el correo con asunto {Subject}.", message.Subject);

            throw new EmailDeliveryException("No se pudo entregar el correo.", exception);
        }
    }
}
