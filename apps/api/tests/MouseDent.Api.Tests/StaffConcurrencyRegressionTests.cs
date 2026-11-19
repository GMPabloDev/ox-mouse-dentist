using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Infrastructure.Auth;
using MouseDent.Api.Auth;
using MouseDent.Application.Auth;
using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class StaffConcurrencyRegressionTests
{
    private const string Password = "Recepcion123!";

    [Fact]
    public async Task Concurrent_edit_returns_conflict_without_restoring_a_changed_email()
    {
        using var factory = new InterleavingFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var oldEmail = StaffTestHelpers.UniqueEmail("edit.old");
        var newEmail = StaffTestHelpers.UniqueEmail("edit.new");
        var id = await factory.SeedAccountAsync(oldEmail, Password, StaffRole.Professional);
        factory.TargetId = id;
        factory.AfterRead = async () =>
        {
            using var concurrent = await StaffTestHelpers.UpdateStaffAsync(admin, id, newEmail, StaffRole.Professional);
            concurrent.StatusCode.ShouldBe(HttpStatusCode.OK);
        };

        using var response = await StaffTestHelpers.UpdateStaffAsync(admin, id, oldEmail, StaffRole.Professional, displayName: "Edición obsoleta");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        problem.ShouldContain("Cuenta modificada");
        var stored = await StaffTestHelpers.FindStaffAsync(admin, newEmail);
        stored.Email.ShouldBe(newEmail);
        factory.Emails.SentTo(oldEmail).ShouldBeEmpty();
        factory.Emails.SentTo(newEmail).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Editing_a_stale_snapshot_does_not_reactivate_or_restore_a_revoked_session()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var address = StaffTestHelpers.UniqueEmail("interleaved.edit");
        var id = await factory.SeedAccountAsync(address, Password, StaffRole.Professional);
        var token = await factory.LoginAsync(address, Password);
        using var session = factory.WithToken(token);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = new InterleavedAccounts(services.GetRequiredService<IStaffAccounts>());
        accounts.AfterRead = async () =>
        {
            using var response = await admin.PatchAsJsonAsync($"/api/staff/{id}/status", new { isActive = false }, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        };
        var access = Access(services, accounts, new StaffAccessOptions { PublicBaseUrl = MouseDentApiFactory.PublicBaseUrl });
        var management = new StaffManagementService(accounts, services.GetRequiredService<ILocales>(), access);

        var result = await management.UpdateAsync(id, new("Nombre nuevo", address, null, StaffRole.Professional, []), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StaffOutcomeStatus.ConcurrentChange);
        using var probe = await session.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        probe.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var stored = await StaffTestHelpers.FindStaffAsync(admin, address);
        stored.Status.ShouldBe(StaffAccountState.Inactiva);
        stored.DisplayName.ShouldNotBe("Nombre nuevo");
    }

    [Fact]
    public async Task Editing_a_stale_snapshot_does_not_restore_sessions_after_password_reset()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var address = StaffTestHelpers.UniqueEmail("interleaved.password");
        var id = await factory.SeedAccountAsync(address, Password, StaffRole.Professional);
        var oldSession = await factory.LoginAsync(address, Password);
        using var session = factory.WithToken(oldSession);
        using var anonymous = factory.CreateClient();
        using var recovery = await StaffTestHelpers.RequestRecoveryAsync(anonymous, address);
        recovery.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var resetToken = factory.Emails.LastTokenTo(address);
        resetToken.ShouldNotBeNullOrWhiteSpace();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = new InterleavedAccounts(services.GetRequiredService<IStaffAccounts>());
        accounts.AfterRead = async () =>
        {
            using var reset = await StaffTestHelpers.ResetPasswordAsync(anonymous, resetToken!, "OtraClave123!");
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        };
        var access = Access(services, accounts, new StaffAccessOptions { PublicBaseUrl = MouseDentApiFactory.PublicBaseUrl });
        var management = new StaffManagementService(accounts, services.GetRequiredService<ILocales>(), access);

        var result = await management.UpdateAsync(id, new("Nombre nuevo", address, null, StaffRole.Professional, []), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StaffOutcomeStatus.ConcurrentChange);
        using var probe = await session.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        probe.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var login = await StaffTestHelpers.LoginAsync(anonymous, address, "OtraClave123!");
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Recovery_from_a_stale_email_does_not_send_a_usable_link_to_the_old_address()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var oldEmail = StaffTestHelpers.UniqueEmail("interleaved.old");
        var newEmail = StaffTestHelpers.UniqueEmail("interleaved.new");
        var id = await factory.SeedAccountAsync(oldEmail, Password, StaffRole.Professional);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = new InterleavedAccounts(services.GetRequiredService<IStaffAccounts>());
        accounts.AfterRead = async () =>
        {
            using var response = await StaffTestHelpers.UpdateStaffAsync(admin, id, newEmail, StaffRole.Professional);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        };
        var access = Access(services, accounts, new StaffAccessOptions { PublicBaseUrl = MouseDentApiFactory.PublicBaseUrl });

        await access.RequestRecoveryAsync(oldEmail, TestContext.Current.CancellationToken);

        factory.Emails.SentTo(oldEmail).ShouldBeEmpty();
        var newToken = factory.Emails.LastTokenTo(newEmail);
        newToken.ShouldNotBeNullOrWhiteSpace();
        using var anonymous = factory.CreateClient();
        using var reset = await StaffTestHelpers.ResetPasswordAsync(anonymous, newToken!, "OtraClave123!");
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Recovery_does_not_issue_after_the_account_is_deactivated()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var address = StaffTestHelpers.UniqueEmail("interleaved.inactive");
        var id = await factory.SeedAccountAsync(address, Password, StaffRole.Professional);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = new InterleavedAccounts(services.GetRequiredService<IStaffAccounts>());
        accounts.AfterRead = async () =>
        {
            using var response = await admin.PatchAsJsonAsync(
                $"/api/staff/{id}/status", new { isActive = false }, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        };
        var access = Access(services, accounts, new StaffAccessOptions { PublicBaseUrl = MouseDentApiFactory.PublicBaseUrl });

        await access.RequestRecoveryAsync(address, TestContext.Current.CancellationToken);

        factory.Emails.SentTo(address).ShouldBeEmpty();
        using var reactivation = await admin.PatchAsJsonAsync(
            $"/api/staff/{id}/status", new { isActive = true }, TestContext.Current.CancellationToken);
        reactivation.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Emails.SentTo(address).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejected_email_change_does_not_invalidate_the_previous_link()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var seeded = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);
        using var anonymous = factory.CreateClient();
        using var recovery = await StaffTestHelpers.RequestRecoveryAsync(anonymous, MouseDentApiFactory.AdminEmail);
        recovery.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var oldToken = factory.Emails.LastTokenTo(MouseDentApiFactory.AdminEmail);
        oldToken.ShouldNotBeNullOrWhiteSpace();

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin, seeded.Id, StaffTestHelpers.UniqueEmail("rejected"), StaffRole.Admin);
        update.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var reset = await StaffTestHelpers.ResetPasswordAsync(anonymous, oldToken!, "OtraClave123!");
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("relative")]
    public async Task Recovery_response_is_generic_when_the_public_url_is_missing(string publicUrl)
    {
        using var factory = new MissingPublicUrlFactory(publicUrl);
        var address = StaffTestHelpers.UniqueEmail("configuration.recovery");
        await factory.SeedAccountAsync(address, Password, StaffRole.Professional);
        using var anonymous = factory.CreateClient();
        using var known = await StaffTestHelpers.RequestRecoveryAsync(anonymous, address);
        using var unknown = await StaffTestHelpers.RequestRecoveryAsync(anonymous, StaffTestHelpers.UniqueEmail("unknown"));

        known.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        unknown.StatusCode.ShouldBe(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(await known.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        factory.Emails.SentTo(address).ShouldBeEmpty();
    }

    [Fact]
    public async Task Email_change_invalidates_old_link_even_when_public_url_is_missing()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var oldEmail = StaffTestHelpers.UniqueEmail("configuration.old");
        var newEmail = StaffTestHelpers.UniqueEmail("configuration.new");
        var id = await factory.SeedAccountAsync(oldEmail, Password, StaffRole.Professional);
        using var anonymous = factory.CreateClient();
        using var recovery = await StaffTestHelpers.RequestRecoveryAsync(anonymous, oldEmail);
        recovery.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var oldToken = factory.Emails.LastTokenTo(oldEmail);
        oldToken.ShouldNotBeNullOrWhiteSpace();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = services.GetRequiredService<IStaffAccounts>();
        var access = Access(services, accounts, new StaffAccessOptions());
        var management = new StaffManagementService(accounts, services.GetRequiredService<ILocales>(), access);

        var result = await management.UpdateAsync(id, new("Persona", newEmail, null, StaffRole.Professional, []), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(StaffOutcomeStatus.InvitationFailed);
        using var reset = await StaffTestHelpers.ResetPasswordAsync(anonymous, oldToken!, "OtraClave123!");
        reset.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var stored = await StaffTestHelpers.FindStaffAsync(admin, newEmail);
        stored.Status.ShouldBe(StaffAccountState.Pendiente);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Local_deactivated_before_assignment_is_not_added(bool edit)
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("interleaved.local");
        var id = edit ? await factory.SeedAccountAsync(email, Password, StaffRole.Professional) : Guid.Empty;
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var accounts = new InterleavedAccounts(services.GetRequiredService<IStaffAccounts>())
        {
            BeforeWrite = () => StaffTestHelpers.SetLocalStatusAsync(admin, local.Id, false),
        };
        var management = new StaffManagementService(accounts, services.GetRequiredService<ILocales>(),
            Access(services, accounts, new StaffAccessOptions { PublicBaseUrl = MouseDentApiFactory.PublicBaseUrl }));
        var details = new StaffAccountDetails("Persona", email, null, StaffRole.Reception, [local.Id]);
        var result = edit
            ? await management.UpdateAsync(id, details, TestContext.Current.CancellationToken)
            : await management.CreateAsync(details, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(StaffOutcomeStatus.LocalsUnavailable);
        var stored = await accounts.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        if (edit) stored!.AssignedLocalIds.ShouldBeEmpty();
        else stored.ShouldBeNull();
    }

    private static StaffAccessService Access(IServiceProvider services, IStaffAccounts accounts, StaffAccessOptions options) =>
        new(accounts, services.GetRequiredService<IStaffAccessLinks>(), services.GetRequiredService<IEmailSender>(), options, services.GetRequiredService<TimeProvider>());

    private sealed class InterleavingFactory() : MouseDentApiFactoryBase(useFileDatabase: true)
    {
        private Func<Task>? _afterRead;
        public Func<Task>? AfterRead { set => _afterRead = value; }
        public Guid TargetId { get; set; }

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IStaffAccounts>();
            services.AddScoped<EfStaffAccounts>();
            services.AddScoped<IStaffAccounts>(provider => new InterleavedAccounts(provider.GetRequiredService<EfStaffAccounts>())
            {
                TargetId = TargetId,
                AfterRead = async () =>
                {
                    var action = Interlocked.Exchange(ref _afterRead, null);
                    if (action is not null) await action();
                },
            });
        }
    }

    private sealed class MissingPublicUrlFactory(string publicUrl) : MouseDentApiFactoryBase(useFileDatabase: true)
    {
        protected override void ConfigureTestSettings(Dictionary<string, string?> settings) =>
            settings["StaffAccess:PublicBaseUrl"] = publicUrl;
    }

    private sealed class InterleavedAccounts(IStaffAccounts inner) : IStaffAccounts
    {
        public Func<Task>? AfterRead { get; set; }
        public Func<Task>? BeforeWrite { get; set; }
        public Guid? TargetId { get; set; }

        private async Task<StaffAccount?> Interleave(Task<StaffAccount?> task)
        {
            var account = await task;
            var action = AfterRead;
            AfterRead = null;
            if (action is not null) await action();
            return account;
        }

        public Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            TargetId is null || TargetId == id
                ? Interleave(inner.FindByIdAsync(id, cancellationToken))
                : inner.FindByIdAsync(id, cancellationToken);
        public Task<StaffAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Interleave(inner.FindByEmailAsync(email, cancellationToken));
        public Task<StaffAccount?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken) => inner.ValidateCredentialsAsync(email, password, cancellationToken);
        public Task<IReadOnlyList<StaffAccount>> ListAsync(string? search, CancellationToken cancellationToken) => inner.ListAsync(search, cancellationToken);
        public async Task<StaffSaveResult> CreateAsync(StaffAccount account, CancellationToken cancellationToken)
        {
            if (BeforeWrite is not null) await BeforeWrite();
            return await inner.CreateAsync(account, cancellationToken);
        }
        public async Task<StaffSaveResult> UpdateAsync(StaffAccount account, StaffAccount expected, CancellationToken cancellationToken)
        {
            if (BeforeWrite is not null) await BeforeWrite();
            return await inner.UpdateAsync(account, expected, cancellationToken);
        }
    }
}
