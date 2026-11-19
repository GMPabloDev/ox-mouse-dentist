using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class StaffSmtpConfigurationTests
{
    [Theory]
    [InlineData(-1, 30)]
    [InlineData(70000, 30)]
    [InlineData(587, -1)]
    [InlineData(587, 3000000)]
    public async Task Invalid_smtp_configuration_keeps_account_and_reports_failure(int port, int timeout)
    {
        using var factory = new InvalidSmtpFactory(port, timeout);
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var email = StaffTestHelpers.UniqueEmail("smtp.invalid");
        using var response = await StaffTestHelpers.CreateStaffAsync(admin, email, StaffRole.Professional);
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await StaffTestHelpers.FindStaffAsync(admin, email)).Status.ShouldBe(MouseDent.Api.Auth.StaffAccountState.Pendiente);
        using var anonymous = factory.CreateClient();
        using var existing = await StaffTestHelpers.RequestRecoveryAsync(anonymous, email);
        using var missing = await StaffTestHelpers.RequestRecoveryAsync(anonymous, StaffTestHelpers.UniqueEmail("missing"));
        existing.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        missing.StatusCode.ShouldBe(existing.StatusCode);
        (await existing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed class InvalidSmtpFactory(int port, int timeout) : MouseDentApiFactoryBase(false)
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.Configure<SmtpOptions>(options => { options.Host = "localhost"; options.FromAddress = "sender@example.com"; options.Port = port; options.TimeoutSeconds = timeout; });
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
    }
}
