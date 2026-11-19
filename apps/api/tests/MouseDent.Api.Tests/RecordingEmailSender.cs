using System.Text.RegularExpressions;
using MouseDent.Application.Auth;

namespace MouseDent.Api.Tests;

/// <summary>
/// Adaptador de correo de las pruebas: guarda lo que la API habría enviado y
/// permite simular un fallo del servidor SMTP. Esta comprobación no sustituye la
/// prueba manual del SMTP configurado.
/// </summary>
internal sealed partial class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    /// <summary>Direcciones cuyo envío falla, como un servidor que rechaza el destinatario.</summary>
    public List<string> FailingAddresses { get; } = [];

    public bool FailsEverySend { get; set; }

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailsEverySend || FailingAddresses.Contains(message.To, StringComparer.OrdinalIgnoreCase))
        {
            throw new EmailDeliveryException("El servidor SMTP de prueba rechazó el envío.");
        }

        lock (_sent)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string address) =>
        [.. Sent.Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Token del enlace del último correo enviado a esa dirección.</summary>
    public string? LastTokenTo(string address)
    {
        var message = SentTo(address).LastOrDefault();
        if (message is null)
        {
            return null;
        }

        var match = TokenPattern().Match(message.Body);

        return match.Success ? match.Groups["token"].Value : null;
    }

    [GeneratedRegex(@"token=(?<token>[A-Za-z0-9_\-]+)")]
    private static partial Regex TokenPattern();
}
