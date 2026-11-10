using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class ApiSmokeTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task Health_reports_healthy_with_database_check()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        payload.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().ShouldBe("database");
    }

    [Fact]
    public async Task OpenApi_document_exposes_bearer_scheme()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        schemes.TryGetProperty("Bearer", out var bearer).ShouldBeTrue();
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
    }

    [Fact]
    public async Task Seeded_admin_can_log_in_and_read_own_profile()
    {
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@mousedent.local", password = "Admin123!" },
            TestContext.Current.CancellationToken);

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginPayload>(TestContext.Current.CancellationToken);
        login.ShouldNotBeNull();
        login.AccessToken.ShouldNotBeNullOrWhiteSpace();
        login.User.Roles.ShouldContain("Administrador");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var meResponse = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<UserPayload>(TestContext.Current.CancellationToken);
        me.ShouldNotBeNull();
        me.Email.ShouldBe("admin@mousedent.local");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_problem_details()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@mousedent.local", password = "no-es-la-clave" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Anonymous_request_to_profile_is_rejected()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record LoginPayload(string AccessToken, DateTimeOffset ExpiresAt, UserPayload User);

    private sealed record UserPayload(Guid Id, string Email, string DisplayName, string[] Roles);
}
