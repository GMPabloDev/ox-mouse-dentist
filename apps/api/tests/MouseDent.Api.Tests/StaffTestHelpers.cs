using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Api.Auth;
using MouseDent.Api.Locales;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>Datos y consultas compartidos por las pruebas de cuentas del personal.</summary>
internal static class StaffTestHelpers
{
    private static readonly string[] Days =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@mousedent.test";

    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    public static async Task<HttpClient> SignInAsync(
        MouseDentApiFactoryBase factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await factory.AccessTokenAsync(email, password));

        return client;
    }

    public static HttpClient WithToken(this MouseDentApiFactoryBase factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>Comprueba el alcance por local en un ámbito nuevo, como una solicitud.</summary>
    public static async Task<bool> CanOperateAsync(
        this MouseDentApiFactoryBase factory,
        Guid accountId,
        Guid localId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LocalAccessService>();

        return await access.CanOperateAsync(accountId, localId, TestContext.Current.CancellationToken);
    }

    /// <summary>Locales alcanzables en un ámbito nuevo, como una solicitud.</summary>
    public static async Task<IReadOnlyList<Guid>> AccessibleLocalsAsync(
        this MouseDentApiFactoryBase factory,
        Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<LocalAccessService>();

        var locals = await access.AccessibleLocalsAsync(accountId, TestContext.Current.CancellationToken);

        return [.. locals.Select(local => local.Id)];
    }

    public static async Task<LocalResponse> CreateLocalAsync(
        HttpClient admin,
        string? name = null,
        bool isActive = true)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(name ?? UniqueName("Sede Personal")), "name" },
            { new StringContent("Av. Siempre Viva 742"), "address" },
            { new StringContent("+51 999 888 777"), "phone" },
            { new StringContent(ScheduleJson()), "schedule" },
        };

        using var response = await admin.PostAsync(
            "/api/locales",
            form,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var local = await response.Content.ReadFromJsonAsync<LocalResponse>(TestContext.Current.CancellationToken);
        local.ShouldNotBeNull();

        if (!isActive)
        {
            await SetLocalStatusAsync(admin, local.Id, isActive: false);
        }

        return local;
    }

    public static async Task SetLocalStatusAsync(HttpClient admin, Guid localId, bool isActive)
    {
        using var response = await admin.PatchAsJsonAsync(
            $"/api/locales/{localId}/status",
            new { isActive },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static Task<HttpResponseMessage> CreateStaffAsync(
        HttpClient admin,
        string email,
        string profile,
        IEnumerable<Guid>? localIds = null,
        string? displayName = null,
        string? phone = "+51 999 888 777") =>
        admin.PostAsJsonAsync(
            "/api/staff",
            Body(email, profile, localIds, displayName, phone),
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> UpdateStaffAsync(
        HttpClient admin,
        Guid accountId,
        string email,
        string profile,
        IEnumerable<Guid>? localIds = null,
        string? displayName = null,
        string? phone = "+51 999 888 777") =>
        admin.PutAsJsonAsync(
            $"/api/staff/{accountId}",
            Body(email, profile, localIds, displayName, phone),
            TestContext.Current.CancellationToken);

    public static object Body(
        string email,
        string profile,
        IEnumerable<Guid>? localIds = null,
        string? displayName = null,
        string? phone = "+51 999 888 777") => new
        {
            displayName = displayName ?? "Persona de pruebas",
            email,
            phone,
            profile,
            assignedLocalIds = localIds?.ToArray() ?? [],
        };

    public static async Task<IReadOnlyList<StaffAccountResponse>> ListStaffAsync(
        HttpClient admin,
        string? search = null)
    {
        var path = search is null ? "/api/staff" : $"/api/staff?search={Uri.EscapeDataString(search)}";

        using var response = await admin.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<StaffAccountResponse[]>(
            TestContext.Current.CancellationToken);
        accounts.ShouldNotBeNull();

        return accounts;
    }

    public static async Task<StaffAccountResponse> FindStaffAsync(HttpClient admin, string email)
    {
        var accounts = await ListStaffAsync(admin, email);

        return accounts.Single(account => account.Email == email);
    }

    public static async Task<StaffAccountResponse> ReadStaffAsync(HttpResponseMessage response)
    {
        var account = await response.Content.ReadFromJsonAsync<StaffAccountResponse>(
            TestContext.Current.CancellationToken);
        account.ShouldNotBeNull();

        return account;
    }

    public static Task<HttpResponseMessage> ResendInvitationAsync(HttpClient admin, Guid accountId) =>
        admin.PostAsync(
            $"/api/staff/{accountId}/invitation",
            content: null,
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> ResetPasswordAsync(
        HttpClient client,
        string token,
        string password) =>
        client.PostAsJsonAsync(
            "/api/auth/password",
            new { token, password },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RequestRecoveryAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/auth/password-recovery",
            new { email },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password },
            TestContext.Current.CancellationToken);

    /// <summary>Crea una cuenta por la API y devuelve su respuesta con el estado esperado.</summary>
    public static async Task<StaffAccountResponse> CreatePendingStaffAsync(
        HttpClient admin,
        string email,
        string profile = StaffRole.Admin,
        IEnumerable<Guid>? localIds = null)
    {
        using var response = await CreateStaffAsync(admin, email, profile, localIds);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadStaffAsync(response);
    }

    public static async Task<Dictionary<string, string[]>> ReadValidationProblemAsync(
        HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldNotBeNull();

        return problem.Errors;
    }

    private static string ScheduleJson() => JsonSerializer.Serialize(
        Days.Select(day => new ScheduleEntry(day, false, "09:00", "18:00")));

    private sealed record ValidationProblemPayload(Dictionary<string, string[]>? Errors);

    private sealed record ScheduleEntry(string Day, bool IsClosed, string? OpensAt, string? ClosesAt);
}
