using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Api.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class StaffLoginConcurrencyTests
{
    private const string OldPassword = "Recepcion123!";
    private const string NewPassword = "NuevaClave123!";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reset_during_password_verification_never_authorizes_the_old_password(bool requiresRehash)
    {
        using var factory = new LoginRaceFactory();
        var address = StaffTestHelpers.UniqueEmail("acceso.intercalado");
        await factory.SeedAccountAsync(address, OldPassword, StaffRole.Professional);
        using var anonymous = factory.CreateClient();
        using var recovery = await StaffTestHelpers.RequestRecoveryAsync(anonymous, address);
        recovery.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var resetToken = factory.Emails.LastTokenTo(address);
        resetToken.ShouldNotBeNullOrWhiteSpace();
        factory.Hasher.Arm(address, requiresRehash);

        var login = Task.Run(() => StaffTestHelpers.LoginAsync(anonymous, address, OldPassword));
        try
        {
            await factory.Hasher.Verified.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            using var reset = await StaffTestHelpers.ResetPasswordAsync(anonymous, resetToken!, NewPassword);
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        finally
        {
            factory.Hasher.Resume.Set();
        }

        using var response = await login;
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
            body.ShouldNotBeNull();
            using var oldSession = factory.WithToken(body.AccessToken);
            using var probe = await oldSession.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
            probe.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        else
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var oldLogin = await StaffTestHelpers.LoginAsync(anonymous, address, OldPassword);
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var newLogin = await StaffTestHelpers.LoginAsync(anonymous, address, NewPassword);
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Successful_login_upgrades_an_outdated_password_hash()
    {
        using var factory = new LoginRaceFactory();
        var address = StaffTestHelpers.UniqueEmail("acceso.rehash");
        var id = await factory.SeedAccountAsync(address, OldPassword, StaffRole.Professional);
        var before = await PasswordHashAsync(factory, id);
        factory.Hasher.Arm(address, requiresRehash: true);
        using var anonymous = factory.CreateClient();
        var login = Task.Run(() => StaffTestHelpers.LoginAsync(anonymous, address, OldPassword));
        try
        {
            await factory.Hasher.Verified.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        }
        finally
        {
            factory.Hasher.Resume.Set();
        }

        using var response = await login;
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await PasswordHashAsync(factory, id);
        after.ShouldNotBe(before);
        using var anotherLogin = await StaffTestHelpers.LoginAsync(anonymous, address, OldPassword);
        anotherLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string?> PasswordHashAsync(LoginRaceFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        return await database.Users.AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => user.PasswordHash)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed class LoginRaceFactory() : MouseDentApiFactoryBase(useFileDatabase: true)
    {
        public PausingPasswordHasher Hasher { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IPasswordHasher<StaffUser>>();
            services.AddSingleton<IPasswordHasher<StaffUser>>(Hasher);
        }
    }

    private sealed class PausingPasswordHasher : IPasswordHasher<StaffUser>
    {
        private readonly PasswordHasher<StaffUser> _inner = new();
        private string? _target;
        private bool _requiresRehash;

        public TaskCompletionSource Verified { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Resume { get; } = new(false);

        public void Arm(string email, bool requiresRehash)
        {
            _requiresRehash = requiresRehash;
            _target = email;
        }

        public string HashPassword(StaffUser user, string password) => _inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(StaffUser user, string hashedPassword, string providedPassword)
        {
            var result = _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
            if (result != PasswordVerificationResult.Failed && user.Email == _target && Interlocked.Exchange(ref _target, null) is not null)
            {
                Verified.TrySetResult();
                if (!Resume.Wait(TimeSpan.FromSeconds(20)))
                {
                    throw new TimeoutException("La prueba no liberó la verificación de contraseña.");
                }

                return _requiresRehash ? PasswordVerificationResult.SuccessRehashNeeded : result;
            }

            return result;
        }
    }
}
