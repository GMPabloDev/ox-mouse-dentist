using System.Net;
using System.Net.Http.Json;
using MouseDent.Api.Catalogo;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class CatalogoEndpointsTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Theory]
    [InlineData("especialidades")]
    [InlineData("servicios")]
    public async Task Status_requires_explicit_boolean_and_preserves_active_record(string resource)
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var suffix = Guid.NewGuid().ToString("N");
        var specialtyResponse = await admin.PostAsJsonAsync("/api/catalogo/especialidades", new { name = "Especialidad " + suffix }, ct);
        var specialty = (await specialtyResponse.Content.ReadFromJsonAsync<EspecialidadResponse>(ct))!;
        var serviceResponse = await admin.PostAsJsonAsync("/api/catalogo/servicios", new { name = "Servicio " + suffix, especialidadId = specialty.Id }, ct);
        var service = (await serviceResponse.Content.ReadFromJsonAsync<ServicioResponse>(ct))!;
        var id = resource == "especialidades" ? specialty.Id : service.Id;
        if (resource == "especialidades")
            (await admin.PatchAsJsonAsync($"/api/catalogo/servicios/{service.Id}/status", new { isActive = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var url = $"/api/catalogo/{resource}/{id}/status";
        using (var emptyBody = await admin.PatchAsJsonAsync(url, new { }, ct))
            await ShouldBeUnreadableBodyProblemAsync(emptyBody);
        using (var nullValue = await admin.PatchAsJsonAsync(url, new { isActive = (bool?)null }, ct))
            await ShouldBeUnreadableBodyProblemAsync(nullValue);
        if (resource == "especialidades")
            (await admin.GetFromJsonAsync<EspecialidadResponse[]>($"/api/catalogo/especialidades?search={suffix}", ct))!.Single().IsActive.ShouldBeTrue();
        else
            (await admin.GetFromJsonAsync<ServicioResponse[]>($"/api/catalogo/servicios?search={suffix}", ct))!.Single().IsActive.ShouldBeTrue();
        (await admin.PatchAsJsonAsync(url, new { isActive = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PatchAsJsonAsync(url, new { isActive = true }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Names_and_descriptions_are_accepted_up_to_the_limits()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var name = new string('n', 90) + suffix;
        var description = new string('d', 500);

        using var specialtyResponse = await admin.PostAsJsonAsync(
            "/api/catalogo/especialidades",
            new { name, description },
            ct);
        specialtyResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<EspecialidadResponse>(ct);
        specialty.ShouldNotBeNull();
        specialty.Name.ShouldBe(name);
        specialty.Description.ShouldBe(description);

        var serviceName = new string('s', 90) + suffix;
        using var serviceResponse = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name = serviceName, description, especialidadId = specialty.Id },
            ct);
        serviceResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var service = await serviceResponse.Content.ReadFromJsonAsync<ServicioResponse>(ct);
        service.ShouldNotBeNull();
        service.Name.ShouldBe(serviceName);
        service.Description.ShouldBe(description);
    }

    [Theory]
    [InlineData("especialidades", 101, 500, "Name")]
    [InlineData("especialidades", 100, 501, "Description")]
    [InlineData("servicios", 101, 500, "Name")]
    [InlineData("servicios", 100, 501, "Description")]
    public async Task Values_over_the_limits_are_rejected_with_a_field_error(
        string resource,
        int nameLength,
        int descriptionLength,
        string field)
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var name = new string('n', nameLength);
        var description = new string('d', descriptionLength);
        object payload = resource == "servicios"
            ? new { name, description, especialidadId = (await CreateEspecialidadAsync(admin, UniqueName("Dental"))).Id }
            : new { name, description };

        using var response = await admin.PostAsJsonAsync($"/api/catalogo/{resource}", payload, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(ct);
        problem.ShouldNotBeNull();
        var errors = problem.Errors;
        errors.ShouldNotBeNull();
        errors.Keys.ShouldBe([field]);
    }

    [Fact]
    public async Task Created_records_publish_no_location_because_there_is_no_get_by_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();

        using var specialtyResponse = await admin.PostAsJsonAsync(
            "/api/catalogo/especialidades",
            new { name = UniqueName("Dental") },
            ct);
        specialtyResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        specialtyResponse.Headers.Location.ShouldBeNull();
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<EspecialidadResponse>(ct);
        specialty.ShouldNotBeNull();

        using var serviceResponse = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name = UniqueName("Limpieza"), especialidadId = specialty.Id },
            ct);
        serviceResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        serviceResponse.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Profiles_filters_and_conflicts_are_enforced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        var suffix = Guid.NewGuid().ToString("N");
        var name = "Dental " + suffix;
        var create = await admin.PostAsJsonAsync("/api/catalogo/especialidades", new { name, description = " prueba " }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var specialty = (await create.Content.ReadFromJsonAsync<EspecialidadResponse>(ct))!;
        specialty.Description.ShouldBe("prueba");
        var createdService = await admin.PostAsJsonAsync("/api/catalogo/servicios", new { name = "Limpieza " + suffix, specialty.Description, especialidadId = specialty.Id }, ct);
        createdService.StatusCode.ShouldBe(HttpStatusCode.Created);
        var service = (await createdService.Content.ReadFromJsonAsync<ServicioResponse>(ct))!;
        (await admin.PostAsJsonAsync("/api/catalogo/especialidades", new { name = " " + name.ToUpperInvariant() + " " }, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/catalogo/especialidades", new { name = "  " }, ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PatchAsJsonAsync($"/api/catalogo/especialidades/{specialty.Id}/status", new { isActive = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PatchAsJsonAsync($"/api/catalogo/servicios/{service.Id}/status", new { isActive = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PatchAsJsonAsync($"/api/catalogo/especialidades/{specialty.Id}/status", new { isActive = false }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PatchAsJsonAsync($"/api/catalogo/servicios/{service.Id}/status", new { isActive = true }, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var filtered = await admin.GetFromJsonAsync<ServicioResponse[]>($"/api/catalogo/servicios?especialidadId={specialty.Id}&isActive=false&search={suffix}", ct);
        filtered!.Select(x => x.Id).ShouldContain(service.Id);
        (await admin.GetFromJsonAsync<ServicioResponse[]>($"/api/catalogo/servicios?especialidadId={Guid.NewGuid()}&search={suffix}", ct))!.ShouldBeEmpty();
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/catalogo/especialidades", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        foreach (var role in new[] { StaffRole.Reception, StaffRole.Professional })
        {
            var email = $"{Guid.NewGuid():N}@test.local";
            const string password = "Password123!";
            await factory.SeedAccountAsync(email, password, role);
            using var client = await StaffTestHelpers.SignInAsync(factory, email, password);
            (await client.GetFromJsonAsync<EspecialidadResponse[]>($"/api/catalogo/especialidades?search={suffix}&isActive=false", ct))!.ShouldBeEmpty();
            (await client.GetFromJsonAsync<ServicioResponse[]>($"/api/catalogo/servicios?search={suffix}&isActive=false", ct))!.ShouldBeEmpty();
            (await client.PostAsJsonAsync("/api/catalogo/especialidades", new { name = "No permitido" }, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected_in_every_operation()
    {
        using var client = factory.CreateClient();

        foreach (var (method, path, payload) in Operations())
        {
            using var request = new HttpRequestMessage(method, path);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    [Theory]
    [InlineData(StaffRole.Reception)]
    [InlineData(StaffRole.Professional)]
    public async Task Every_write_is_forbidden_for_reception_and_professionals(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var specialty = await CreateEspecialidadAsync(admin, UniqueName("Dental"));
        var service = await CreateServicioAsync(admin, UniqueName("Limpieza"), specialty.Id);
        using var client = await SignInAsync(role);
        var writes = new (HttpMethod Method, string Path, object? Payload)[]
        {
            (HttpMethod.Post, "/api/catalogo/especialidades", new { name = "No permitido" }),
            (HttpMethod.Put, $"/api/catalogo/especialidades/{specialty.Id}", new { name = "No permitido" }),
            (HttpMethod.Patch, $"/api/catalogo/especialidades/{specialty.Id}/status", new { isActive = false }),
            (HttpMethod.Post, "/api/catalogo/servicios", new { name = "No permitido", especialidadId = specialty.Id }),
            (HttpMethod.Put, $"/api/catalogo/servicios/{service.Id}", new { name = service.Name, especialidadId = specialty.Id }),
            (HttpMethod.Patch, $"/api/catalogo/servicios/{service.Id}/status", new { isActive = false }),
        };

        foreach (var (method, path, payload) in writes)
        {
            using var request = new HttpRequestMessage(method, path);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }

            using var response = await client.SendAsync(request, ct);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        var storedSpecialty = await FindEspecialidadAsync(admin, specialty.Name);
        storedSpecialty.Name.ShouldBe(specialty.Name);
        storedSpecialty.IsActive.ShouldBeTrue();
        var storedService = await FindServicioAsync(admin, service.Name);
        storedService.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(StaffRole.Reception)]
    [InlineData(StaffRole.Professional)]
    public async Task Reception_and_professionals_receive_the_active_catalog(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var activeSpecialty = await CreateEspecialidadAsync(admin, $"Dental {suffix}");
        var activeService = await CreateServicioAsync(admin, $"Limpieza {suffix}", activeSpecialty.Id);
        var inactiveSpecialty = await CreateEspecialidadAsync(admin, $"Ortodoncia {suffix}");
        await SetEspecialidadStatusAsync(admin, inactiveSpecialty.Id, false);
        var inactiveService = await CreateServicioAsync(admin, $"Brackets {suffix}", activeSpecialty.Id);
        await SetServicioStatusAsync(admin, inactiveService.Id, false);
        using var client = await SignInAsync(role);

        var specialties = await client.GetFromJsonAsync<EspecialidadResponse[]>(
            $"/api/catalogo/especialidades?search={suffix}",
            ct);
        specialties.ShouldNotBeNull().Select(x => x.Name).ShouldBe([activeSpecialty.Name]);

        var services = await client.GetFromJsonAsync<ServicioResponse[]>(
            $"/api/catalogo/servicios?search={suffix}",
            ct);
        services.ShouldNotBeNull().Select(x => x.Name).ShouldBe([activeService.Name]);
    }

    [Fact]
    public async Task A_specialty_is_edited_with_not_found_and_duplicate_problems()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var specialty = await CreateEspecialidadAsync(admin, UniqueName("Dental"));
        var other = await CreateEspecialidadAsync(admin, UniqueName("Ortodoncia"));

        using var update = await admin.PutAsJsonAsync(
            $"/api/catalogo/especialidades/{specialty.Id}",
            new { name = $"  {specialty.Name.ToUpperInvariant()}  ", description = "  Primera  " },
            ct);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = await update.Content.ReadFromJsonAsync<EspecialidadResponse>(ct);
        edited.ShouldNotBeNull();
        edited.Name.ShouldBe(specialty.Name.ToUpperInvariant());
        edited.Description.ShouldBe("Primera");

        using var missing = await admin.PutAsJsonAsync(
            $"/api/catalogo/especialidades/{Guid.NewGuid()}",
            new { name = UniqueName("Perdida") },
            ct);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldBeProblemAsync(
            missing,
            "Registro no encontrado",
            "No existe el registro solicitado.");

        using var duplicate = await admin.PutAsJsonAsync(
            $"/api/catalogo/especialidades/{specialty.Id}",
            new { name = other.Name.ToLowerInvariant() },
            ct);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBeProblemAsync(
            duplicate,
            "Nombre duplicado",
            "Ya existe un registro con ese nombre, incluso entre los inactivos.");

        var stored = await FindEspecialidadAsync(admin, other.Name);
        stored.Name.ShouldBe(other.Name);
        stored.Description.ShouldBe(other.Description);
    }

    [Fact]
    public async Task A_service_changes_specialty_and_the_destination_rules_are_reported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var origin = await CreateEspecialidadAsync(admin, UniqueName("Dental"));
        var destination = await CreateEspecialidadAsync(admin, UniqueName("Ortodoncia"));
        var closed = await CreateEspecialidadAsync(admin, UniqueName("Cerrada"));
        var service = await CreateServicioAsync(admin, UniqueName("Limpieza"), origin.Id);
        var neighbour = await CreateServicioAsync(admin, UniqueName("Brackets"), destination.Id);

        using var moved = await admin.PutAsJsonAsync(
            $"/api/catalogo/servicios/{service.Id}",
            new { name = service.Name, description = "Movido", especialidadId = destination.Id },
            ct);
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = await moved.Content.ReadFromJsonAsync<ServicioResponse>(ct);
        edited.ShouldNotBeNull();
        edited.EspecialidadId.ShouldBe(destination.Id);
        edited.Description.ShouldBe("Movido");

        // El nombre ocupado en el destino también cuenta si ese registro está inactivo.
        await SetServicioStatusAsync(admin, neighbour.Id, false);
        using var duplicate = await admin.PutAsJsonAsync(
            $"/api/catalogo/servicios/{service.Id}",
            new { name = neighbour.Name, especialidadId = destination.Id },
            ct);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBeProblemAsync(
            duplicate,
            "Nombre duplicado",
            "Ya existe un registro con ese nombre, incluso entre los inactivos.");

        await SetEspecialidadStatusAsync(admin, closed.Id, false);
        using var inactive = await admin.PutAsJsonAsync(
            $"/api/catalogo/servicios/{service.Id}",
            new { name = service.Name, especialidadId = closed.Id },
            ct);
        inactive.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBeProblemAsync(
            inactive,
            "La especialidad de destino está inactiva",
            "Selecciona una especialidad activa.");

        using var createdInInactive = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name = UniqueName("Nuevo"), especialidadId = closed.Id },
            ct);
        createdInInactive.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBeProblemAsync(
            createdInInactive,
            "La especialidad de destino está inactiva",
            "Selecciona una especialidad activa.");

        var stored = await FindServicioAsync(admin, service.Name);
        stored.EspecialidadId.ShouldBe(destination.Id);
    }

    [Fact]
    public async Task A_service_name_must_be_unique_inside_its_specialty_only()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminAsync();
        var first = await CreateEspecialidadAsync(admin, UniqueName("Dental"));
        var second = await CreateEspecialidadAsync(admin, UniqueName("Ortodoncia"));
        var name = UniqueName("Limpieza");

        await CreateServicioAsync(admin, name, first.Id);
        await CreateServicioAsync(admin, name, second.Id);

        using var duplicate = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name = $"  {name.ToUpperInvariant()}  ", especialidadId = first.Id },
            ct);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBeProblemAsync(
            duplicate,
            "Nombre duplicado",
            "Ya existe un registro con ese nombre, incluso entre los inactivos.");
    }

    [Fact]
    public async Task A_service_without_specialty_is_rejected_with_a_clear_message()
    {
        using var admin = await AdminAsync();

        using var response = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name = UniqueName("Limpieza"), especialidadId = Guid.Empty },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        var errors = problem.Errors;
        errors.ShouldNotBeNull();
        errors["EspecialidadId"].ShouldBe(["Selecciona una especialidad."]);
    }

    private static IEnumerable<(HttpMethod Method, string Path, object? Payload)> Operations()
    {
        var id = Guid.NewGuid();
        var specialtyId = Guid.NewGuid();
        var body = new { name = "No permitido", description = (string?)null, especialidadId = specialtyId };

        yield return (HttpMethod.Get, "/api/catalogo/especialidades", null);
        yield return (HttpMethod.Get, "/api/catalogo/servicios", null);
        yield return (HttpMethod.Post, "/api/catalogo/especialidades", new { name = "No permitido" });
        yield return (HttpMethod.Put, $"/api/catalogo/especialidades/{id}", new { name = "No permitido" });
        yield return (HttpMethod.Patch, $"/api/catalogo/especialidades/{id}/status", new { isActive = false });
        yield return (HttpMethod.Post, "/api/catalogo/servicios", body);
        yield return (HttpMethod.Put, $"/api/catalogo/servicios/{id}", body);
        yield return (HttpMethod.Patch, $"/api/catalogo/servicios/{id}/status", new { isActive = false });
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);

    private async Task<HttpClient> SignInAsync(string role)
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        const string password = "Password123!";
        await factory.SeedAccountAsync(email, password, role);

        return await StaffTestHelpers.SignInAsync(factory, email, password);
    }

    private static async Task<EspecialidadResponse> CreateEspecialidadAsync(HttpClient admin, string name)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/catalogo/especialidades",
            new { name },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<EspecialidadResponse>(
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        return created;
    }

    private static async Task<ServicioResponse> CreateServicioAsync(
        HttpClient admin,
        string name,
        Guid especialidadId)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/catalogo/servicios",
            new { name, especialidadId },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ServicioResponse>(
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        return created;
    }

    private static async Task SetEspecialidadStatusAsync(HttpClient admin, Guid id, bool isActive)
    {
        using var response = await admin.PatchAsJsonAsync(
            $"/api/catalogo/especialidades/{id}/status",
            new { isActive },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task SetServicioStatusAsync(HttpClient admin, Guid id, bool isActive)
    {
        using var response = await admin.PatchAsJsonAsync(
            $"/api/catalogo/servicios/{id}/status",
            new { isActive },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<EspecialidadResponse> FindEspecialidadAsync(HttpClient admin, string name)
    {
        var stored = await admin.GetFromJsonAsync<EspecialidadResponse[]>(
            $"/api/catalogo/especialidades?search={Uri.EscapeDataString(name)}",
            TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();

        return stored.Single();
    }

    private static async Task<ServicioResponse> FindServicioAsync(HttpClient admin, string name)
    {
        var stored = await admin.GetFromJsonAsync<ServicioResponse[]>(
            $"/api/catalogo/servicios?search={Uri.EscapeDataString(name)}",
            TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();

        return stored.Single();
    }

    private static async Task ShouldBeProblemAsync(
        HttpResponseMessage response,
        string title,
        string detail)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
            TestContext.Current.CancellationToken);

        problem.ShouldNotBeNull();
        problem.Title.ShouldBe(title);
        problem.Detail.ShouldBe(detail);
    }

    /// <summary>
    /// Un cuerpo que no se puede leer también responde ProblemDetails, pero el
    /// título y el detalle los genera el framework, no la API del catálogo.
    /// </summary>
    private static async Task ShouldBeUnreadableBodyProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
            TestContext.Current.CancellationToken);

        problem.ShouldNotBeNull();
        problem.Title.ShouldNotBeNullOrWhiteSpace();
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private sealed record ProblemPayload(string? Title, string? Detail);

    private sealed record ValidationProblemPayload(Dictionary<string, string[]>? Errors);
}
