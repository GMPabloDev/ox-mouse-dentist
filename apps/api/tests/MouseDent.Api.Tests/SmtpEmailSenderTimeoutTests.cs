using System.Net;
using System.Net.Sockets;
using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MouseDent.Application.Auth;
using MouseDent.Infrastructure.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class SmtpEmailSenderTimeoutTests
{
    [Fact]
    public async Task Stalled_smtp_server_fails_within_the_configured_deadline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1", Port = port, TimeoutSeconds = 1, FromAddress = "sender@example.test",
        }), NullLogger<SmtpEmailSender>.Instance);

        var sending = sender.SendAsync(new EmailMessage("recipient@example.test", "test", "test"), TestContext.Current.CancellationToken);
        using var peer = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<EmailDeliveryException>(async () => await sending.WaitAsync(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken));
        exception.InnerException.ShouldBeOfType<SmtpException>();
    }
}
