using System.Net;
using System.Net.Http.Json;
using MouseDent.Api.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class StaffAccountEndpointsTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task Anonymous_requests_are_rejected_in_every_operation()
    {
        using var client = factory.CreateClient();

        foreach (var (method, path) in Operations())
        {
            using var response = await client.SendAsync(
                new HttpRequestMessage(method, path),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    [Fact]
    public async Task Requests_from_reception_are_forbidden_in_every_operation()
    {
        await factory.SeedReceptionAccountAsync();
        using var client = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.ReceptionEmail,
            MouseDentApiFactory.ReceptionPassword);

        foreach (var (method, path) in Operations())
        {
            using var response = await client.SendAsync(
                new HttpRequestMessage(method, path),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{method} {path}");
        }
    }

    [Fact]
    public async Task A_reception_account_is_created_pending_with_its_active_locals()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("recepcion.nueva");

        using var response = await StaffTestHelpers.CreateStaffAsync(
            admin,
            email,
            StaffRole.Reception,
            [local.Id],
            displayName: "Recepción Nueva");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await StaffTestHelpers.ReadStaffAsync(response);
        created.Email.ShouldBe(email);
        created.DisplayName.ShouldBe("Recepción Nueva");
        created.Phone.ShouldBe("+51 999 888 777");
        created.Profile.ShouldBe(StaffRole.Reception);
        created.Status.ShouldBe(StaffAccountState.Pendiente);
        created.AssignedLocalIds.ShouldBe([local.Id]);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldBe($"/api/staff/{created.Id}");

        var listed = await StaffTestHelpers.FindStaffAsync(admin, email);
        listed.Status.ShouldBe(StaffAccountState.Pendiente);
        listed.AssignedLocalIds.ShouldBe([local.Id]);

        using var anonymous = factory.CreateClient();
        using var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Recepcion123!" },
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_email_with_accents_is_created_like_any_other()
    {
        using var admin = await AdminAsync();
        var email = $"jose.ramirez.{Guid.NewGuid():N}"[..24] + "@mousedent.test";
        var accented = email.Replace("jose", "josé");

        using var response = await StaffTestHelpers.CreateStaffAsync(admin, accented, StaffRole.Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await StaffTestHelpers.ReadStaffAsync(response)).Email.ShouldBe(accented);
    }

    [Fact]
    public async Task A_professional_account_cannot_be_created_from_the_staff_module()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("profesional.rechazado");

        using var response = await StaffTestHelpers.CreateStaffAsync(admin, email, StaffRole.Professional);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Perfil Profesional fuera del módulo de personal");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();

        // El rechazo no crea la cuenta ni envía una invitación.
        (await StaffTestHelpers.ListStaffAsync(admin, email)).ShouldBeEmpty();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_a_reception_account_to_professional_is_rejected_without_editing_it()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("recepcion.a.profesional");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(
            admin,
            email,
            StaffRole.Reception,
            [local.Id]);

        using var response = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            email,
            StaffRole.Professional);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Perfil Profesional fuera del módulo de personal");

        var stored = await StaffTestHelpers.FindStaffAsync(admin, email);
        stored.Profile.ShouldBe(StaffRole.Reception);
        stored.AssignedLocalIds.ShouldBe([local.Id]);
        stored.Status.ShouldBe(StaffAccountState.Pendiente);
    }

    [Fact]
    public async Task A_new_assignment_to_an_inactive_local_is_rejected()
    {
        using var admin = await AdminAsync();
        var active = await StaffTestHelpers.CreateLocalAsync(admin);
        var inactive = await StaffTestHelpers.CreateLocalAsync(admin, isActive: false);
        var email = StaffTestHelpers.UniqueEmail("recepcion.inactiva");

        using var response = await StaffTestHelpers.CreateStaffAsync(
            admin,
            email,
            StaffRole.Reception,
            [active.Id, inactive.Id]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var errors = await StaffTestHelpers.ReadValidationProblemAsync(response);
        errors.ShouldContainKey("AssignedLocalIds");

        var listed = await StaffTestHelpers.ListStaffAsync(admin, email);
        listed.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_reception_account_with_an_unknown_local_is_rejected()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("recepcion.inexistente");

        using var response = await StaffTestHelpers.CreateStaffAsync(
            admin,
            email,
            StaffRole.Reception,
            [Guid.NewGuid()]);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = await StaffTestHelpers.ReadValidationProblemAsync(response);
        errors.ShouldContainKey("AssignedLocalIds");
        (await StaffTestHelpers.ListStaffAsync(admin, email)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_data_is_reported_by_field()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("validacion");

        var cases = new (object Body, string Field)[]
        {
            (StaffTestHelpers.Body(email, StaffRole.Admin, displayName: ""), "DisplayName"),
            (StaffTestHelpers.Body("no-es-un-correo", StaffRole.Admin), "Email"),
            (StaffTestHelpers.Body(email, "Coordinador"), "Profile"),
            (StaffTestHelpers.Body(email, StaffRole.Reception), "AssignedLocalIds"),
            (StaffTestHelpers.Body(email, StaffRole.Admin, [local.Id]), "AssignedLocalIds"),
        };

        foreach (var (body, field) in cases)
        {
            using var response = await admin.PostAsJsonAsync(
                "/api/staff",
                body,
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, field);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
            var errors = await StaffTestHelpers.ReadValidationProblemAsync(response);
            errors.ShouldContainKey(field);
        }
    }

    [Fact]
    public async Task A_repeated_email_is_rejected_even_when_the_account_is_inactive()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("correo.repetido");

        using var created = await StaffTestHelpers.CreateStaffAsync(admin, email, StaffRole.Admin);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = await StaffTestHelpers.ReadStaffAsync(created);

        using var deactivate = await admin.PatchAsJsonAsync(
            $"/api/staff/{account.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StaffTestHelpers.ReadStaffAsync(deactivate)).Status.ShouldBe(StaffAccountState.Inactiva);

        using var duplicate = await StaffTestHelpers.CreateStaffAsync(admin, email.ToUpperInvariant(), StaffRole.Admin);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Editing_a_reception_account_applies_the_new_assignments()
    {
        using var admin = await AdminAsync();
        var first = await StaffTestHelpers.CreateLocalAsync(admin);
        var second = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("recepcion.edicion");

        using var created = await StaffTestHelpers.CreateStaffAsync(
            admin,
            email,
            StaffRole.Reception,
            [first.Id]);
        var account = await StaffTestHelpers.ReadStaffAsync(created);

        using var response = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            email,
            StaffRole.Reception,
            [second.Id],
            displayName: "Recepción Editada");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await StaffTestHelpers.ReadStaffAsync(response);
        updated.DisplayName.ShouldBe("Recepción Editada");
        updated.AssignedLocalIds.ShouldBe([second.Id]);
        updated.Status.ShouldBe(StaffAccountState.Pendiente);

        var listed = await StaffTestHelpers.FindStaffAsync(admin, email);
        listed.AssignedLocalIds.ShouldBe([second.Id]);
        listed.DisplayName.ShouldBe("Recepción Editada");
    }

    [Fact]
    public async Task The_staff_list_can_be_searched_by_name_and_by_email()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("busqueda");
        var displayName = StaffTestHelpers.UniqueName("Persona Buscada");

        using var created = await StaffTestHelpers.CreateStaffAsync(
            admin,
            email,
            StaffRole.Admin,
            displayName: displayName);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var byName = await StaffTestHelpers.ListStaffAsync(admin, displayName.ToUpperInvariant());
        byName.ShouldContain(account => account.Email == email);

        var byEmail = await StaffTestHelpers.ListStaffAsync(admin, email[..12]);
        byEmail.ShouldContain(account => account.Email == email);

        var byOther = await StaffTestHelpers.ListStaffAsync(admin, StaffTestHelpers.UniqueEmail("sin.resultados"));
        byOther.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deactivating_and_reactivating_an_account_keeps_its_data()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("recepcion.estado");
        var accountId = await factory.SeedAccountAsync(
            email,
            "Recepcion123!",
            StaffRole.Reception);

        using var assigned = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Reception,
            [local.Id]);
        assigned.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deactivated = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);

        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inactive = await StaffTestHelpers.ReadStaffAsync(deactivated);
        inactive.Status.ShouldBe(StaffAccountState.Inactiva);
        inactive.AssignedLocalIds.ShouldBe([local.Id]);
        inactive.Email.ShouldBe(email);

        using var reactivated = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);

        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var active = await StaffTestHelpers.ReadStaffAsync(reactivated);
        active.Status.ShouldBe(StaffAccountState.Activa);
        active.AssignedLocalIds.ShouldBe([local.Id]);
    }

    [Fact]
    public async Task The_only_available_administrator_cannot_be_deactivated()
    {
        using var admin = await AdminAsync();
        var seeded = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);

        var pendingAdminEmail = StaffTestHelpers.UniqueEmail("admin.pendiente");
        using var createdPending = await StaffTestHelpers.CreateStaffAsync(
            admin,
            pendingAdminEmail,
            StaffRole.Admin);
        createdPending.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await StaffTestHelpers.ReadStaffAsync(createdPending)).Status.ShouldBe(StaffAccountState.Pendiente);

        using var response = await admin.PatchAsJsonAsync(
            $"/api/staff/{seeded.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Último administrador disponible");

        var stored = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);
        stored.Status.ShouldBe(StaffAccountState.Activa);
    }

    [Fact]
    public async Task Operations_on_an_unknown_account_return_not_found()
    {
        using var admin = await AdminAsync();
        var id = Guid.NewGuid();
        var email = StaffTestHelpers.UniqueEmail("inexistente");

        using var update = await StaffTestHelpers.UpdateStaffAsync(admin, id, email, StaffRole.Admin);
        using var status = await admin.PatchAsJsonAsync(
            $"/api/staff/{id}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);

        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        status.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);

    private static IEnumerable<(HttpMethod Method, string Path)> Operations()
    {
        var id = Guid.NewGuid();

        yield return (HttpMethod.Get, "/api/staff");
        yield return (HttpMethod.Post, "/api/staff");
        yield return (HttpMethod.Put, $"/api/staff/{id}");
        yield return (HttpMethod.Patch, $"/api/staff/{id}/status");
    }

    private sealed record ProblemPayload(string? Title, string? Detail);
}
