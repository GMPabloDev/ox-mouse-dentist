using System.Net;
using System.Net.Http.Json;
using MouseDent.Api.Catalogo;
using MouseDent.Api.Locales;
using MouseDent.Api.Profesionales;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>Datos y consultas compartidos por las pruebas de profesionales.</summary>
internal static class ProfesionalTestHelpers
{
    public const string NewPassword = "Profesional123!";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<EspecialidadResponse> CreateEspecialidadAsync(
        HttpClient admin,
        bool isActive = true)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/catalogo/especialidades",
            new { name = StaffTestHelpers.UniqueName("Especialidad") },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var especialidad = await response.Content.ReadFromJsonAsync<EspecialidadResponse>(Token);
        especialidad.ShouldNotBeNull();

        if (!isActive)
        {
            await SetEspecialidadStatusAsync(admin, especialidad.Id, isActive: false);
        }

        return especialidad;
    }

    public static async Task SetEspecialidadStatusAsync(HttpClient admin, Guid especialidadId, bool isActive)
    {
        using var response = await admin.PatchAsJsonAsync(
            $"/api/catalogo/especialidades/{especialidadId}/status",
            new { isActive },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static Task<LocalResponse> CreateLocalAsync(HttpClient admin, bool isActive = true) =>
        StaffTestHelpers.CreateLocalAsync(admin, isActive: isActive);

    public static Task SetLocalStatusAsync(HttpClient admin, Guid localId, bool isActive) =>
        StaffTestHelpers.SetLocalStatusAsync(admin, localId, isActive);

    public static Task<HttpResponseMessage> CreateAsync(
        HttpClient admin,
        string email,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        string? colegiatura = null,
        string? displayName = null) =>
        admin.PostAsJsonAsync(
            "/api/profesionales",
            new
            {
                email,
                displayName = displayName ?? "Profesional de pruebas",
                colegiatura,
                especialidadIds = especialidadIds.ToArray(),
                localIds = localIds.ToArray(),
            },
            Token);

    public static async Task<ProfesionalResponse> CreateProfesionalAsync(
        HttpClient admin,
        string email,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        string? colegiatura = null,
        string? displayName = null)
    {
        using var response = await CreateAsync(admin, email, especialidadIds, localIds, colegiatura, displayName);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync(response);
    }

    public static async Task<ProfesionalResponse> ReadAsync(HttpResponseMessage response)
    {
        var profesional = await response.Content.ReadFromJsonAsync<ProfesionalResponse>(Token);
        profesional.ShouldNotBeNull();

        return profesional;
    }

    public static async Task<ProfesionalResponse> GetAsync(HttpClient admin, Guid id)
    {
        using var response = await admin.GetAsync($"/api/profesionales/{id}", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    public static async Task<ProfesionalResponse[]> ListAsync(HttpClient admin, string? query = null)
    {
        var path = query is null ? "/api/profesionales" : $"/api/profesionales?{query}";
        using var response = await admin.GetAsync(path, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profesionales = await response.Content.ReadFromJsonAsync<ProfesionalResponse[]>(Token);
        profesionales.ShouldNotBeNull();

        return profesionales;
    }

    public static Task<HttpResponseMessage> UpdateAsync(
        HttpClient admin,
        Guid id,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        string? colegiatura = null) =>
        admin.PutAsJsonAsync(
            $"/api/profesionales/{id}",
            new { colegiatura, especialidadIds = especialidadIds.ToArray(), localIds = localIds.ToArray() },
            Token);

    public static Task<HttpResponseMessage> SetFichaStatusAsync(HttpClient admin, Guid id, bool isActive) =>
        admin.PatchAsJsonAsync($"/api/profesionales/{id}/status", new { isActive }, Token);

    public static Task<HttpResponseMessage> ResendInvitationAsync(HttpClient admin, Guid id) =>
        admin.PostAsync($"/api/profesionales/{id}/invitation", content: null, Token);

    public static async Task SetAccountStatusAsync(HttpClient admin, Guid accountId, bool isActive)
    {
        using var response = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Establece la contraseña de una cuenta pendiente con el enlace de la última
    /// invitación enviada, como haría la persona invitada.
    /// </summary>
    public static async Task ActivateAccountAsync(MouseDentApiFactoryBase factory, string email)
    {
        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();

        using var anonymous = factory.CreateClient();
        using var response = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Crea una cuenta Profesional con contraseña y la vincula con una ficha por correo.</summary>
    public static async Task<(Guid AccountId, ProfesionalResponse Ficha)> LinkedActiveProfesionalAsync(
        MouseDentApiFactoryBase factory,
        HttpClient admin,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        string? colegiatura = null)
    {
        var email = StaffTestHelpers.UniqueEmail("profesional.activo");
        var accountId = await factory.SeedAccountAsync(email, NewPassword, "Profesional");
        var ficha = await CreateProfesionalAsync(admin, email, especialidadIds, localIds, colegiatura);

        return (accountId, ficha);
    }
}
