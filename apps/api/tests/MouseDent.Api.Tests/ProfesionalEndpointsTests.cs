using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Api.Auth;
using MouseDent.Api.Profesionales;
using MouseDent.Application.Auth;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Contrato de administración de profesionales contra la API real: autorización por
/// perfil, lista con búsqueda y filtros, alta con cuenta e invitación, edición con
/// la regla del mínimo, cambio de estado y reenvío. Los errores se comprueban como
/// <c>ProblemDetails</c> o <c>ValidationProblemDetails</c>.
/// </summary>
public sealed class ProfesionalEndpointsTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    private const string ProfessionalEmail = "profesional.autorizacion@mousedent.test";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string, string?, int> Operations => new()
    {
        { "GET", "/api/profesionales", null, StatusCodes.Status200OK },
        { "GET", "/api/profesionales/{id}", null, StatusCodes.Status200OK },
        { "POST", "/api/profesionales", "alta", StatusCodes.Status201Created },
        { "PUT", "/api/profesionales/{id}", "edicion", StatusCodes.Status200OK },
        { "PATCH", "/api/profesionales/{id}/status", "estado", StatusCodes.Status200OK },
        { "POST", "/api/profesionales/{id}/invitation", null, StatusCodes.Status200OK },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Every_endpoint_requires_administration(string method, string template, string? kind, int expected)
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("autorizacion"),
            [especialidad.Id],
            [local.Id]);
        var path = template.Replace("{id}", ficha.Id.ToString(), StringComparison.Ordinal);
        object? payload = kind switch
        {
            "alta" => new
            {
                email = StaffTestHelpers.UniqueEmail("autorizacion.alta"),
                displayName = "Profesional de pruebas",
                colegiatura = (string?)null,
                especialidadIds = new[] { especialidad.Id },
                localIds = new[] { local.Id },
            },
            "edicion" => new
            {
                colegiatura = (string?)null,
                especialidadIds = new[] { especialidad.Id },
                localIds = new[] { local.Id },
            },
            "estado" => new { isActive = true },
            _ => null,
        };

        using (var anonymous = factory.CreateClient())
        {
            (await SendAsync(anonymous, method, path, payload)).StatusCode
                .ShouldBe(HttpStatusCode.Unauthorized);
        }

        await factory.SeedReceptionAccountAsync();
        using (var reception = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.ReceptionEmail,
            MouseDentApiFactory.ReceptionPassword))
        {
            (await SendAsync(reception, method, path, payload)).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
        }

        await factory.SeedAccountAsync(ProfessionalEmail, ProfesionalTestHelpers.NewPassword, StaffRole.Professional);
        using (var professional = factory.WithToken(
            await factory.AccessTokenAsync(ProfessionalEmail, ProfesionalTestHelpers.NewPassword)))
        {
            (await SendAsync(professional, method, path, payload)).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
        }

        using var response = await SendAsync(admin, method, path, payload);

        ((int)response.StatusCode).ShouldBe(expected);
    }

    [Fact]
    public async Task The_list_can_be_searched_and_filtered_by_specialty_local_ficha_and_account()
    {
        using var admin = await AdminAsync();
        var odontologia = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var ortodoncia = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var centro = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var norte = await ProfesionalTestHelpers.CreateLocalAsync(admin);

        var pendienteEmail = StaffTestHelpers.UniqueEmail("lista.pendiente");
        var pendiente = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            pendienteEmail,
            [odontologia.Id],
            [centro.Id],
            "COP LISTA 1",
            "Ana Torres");

        var activaEmail = StaffTestHelpers.UniqueEmail("lista.activa");
        var activaAccountId = await factory.SeedAccountAsync(
            activaEmail,
            ProfesionalTestHelpers.NewPassword,
            StaffRole.Professional);
        var activa = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            activaEmail,
            [ortodoncia.Id],
            [norte.Id],
            "COP LISTA 2");

        var inactivaEmail = StaffTestHelpers.UniqueEmail("lista.inactiva");
        var inactiva = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            inactivaEmail,
            [odontologia.Id],
            [norte.Id]);
        using (var status = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, inactiva.Id, isActive: false))
        {
            status.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await ProfesionalTestHelpers.SetAccountStatusAsync(admin, inactiva.StaffAccountId, isActive: false);

        // La ficha devuelve la cuenta, la colegiatura, ambos estados y las asignaciones.
        var detalle = await ProfesionalTestHelpers.GetAsync(admin, pendiente.Id);
        detalle.ShouldSatisfyAllConditions(
            resumen => resumen.StaffAccountId.ShouldBe(pendiente.StaffAccountId),
            resumen => resumen.DisplayName.ShouldBe("Ana Torres"),
            resumen => resumen.Email.ShouldBe(pendienteEmail),
            resumen => resumen.Phone.ShouldBeNull(),
            resumen => resumen.Colegiatura.ShouldBe("COP LISTA 1"),
            resumen => resumen.IsActive.ShouldBeTrue(),
            resumen => resumen.AccountStatus.ShouldBe(StaffAccountState.Pendiente),
            resumen => resumen.InvitationFailed.ShouldBeFalse(),
            resumen => resumen.Especialidades.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                especialidad => especialidad.Id.ShouldBe(odontologia.Id),
                especialidad => especialidad.Name.ShouldBe(odontologia.Name),
                especialidad => especialidad.IsActive.ShouldBeTrue()),
            resumen => resumen.Locales.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                local => local.Id.ShouldBe(centro.Id),
                local => local.Name.ShouldBe(centro.Name),
                local => local.IsActive.ShouldBeTrue()));

        // La lista devuelve la misma forma que la ficha.
        var listada = (await ProfesionalTestHelpers.ListAsync(admin, Query("search", pendienteEmail)))
            .ShouldHaveSingleItem();
        ShouldBeSameFicha(detalle, listada);

        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", "Ana Torres")))
            .ShouldContain(resumen => resumen.Id == pendiente.Id);

        var porEspecialidad = await ProfesionalTestHelpers.ListAsync(
            admin,
            Query("especialidadId", odontologia.Id));
        porEspecialidad.ShouldContain(resumen => resumen.Id == pendiente.Id);
        porEspecialidad.ShouldContain(resumen => resumen.Id == inactiva.Id);
        porEspecialidad.ShouldNotContain(resumen => resumen.Id == activa.Id);

        var porLocal = await ProfesionalTestHelpers.ListAsync(admin, Query("localId", norte.Id));
        porLocal.ShouldContain(resumen => resumen.Id == activa.Id);
        porLocal.ShouldContain(resumen => resumen.Id == inactiva.Id);
        porLocal.ShouldNotContain(resumen => resumen.Id == pendiente.Id);

        var inactivas = await ProfesionalTestHelpers.ListAsync(admin, "fichaActiva=false");
        inactivas.ShouldContain(resumen => resumen.Id == inactiva.Id);
        inactivas.ShouldNotContain(resumen => resumen.Id == pendiente.Id);
        inactivas.ShouldAllBe(resumen => !resumen.IsActive);

        var activas = await ProfesionalTestHelpers.ListAsync(admin, "fichaActiva=true");
        activas.ShouldContain(resumen => resumen.Id == pendiente.Id);
        activas.ShouldNotContain(resumen => resumen.Id == inactiva.Id);
        activas.ShouldAllBe(resumen => resumen.IsActive);

        var pendientes = await ProfesionalTestHelpers.ListAsync(admin, "accountStatus=Pendiente");
        pendientes.ShouldContain(resumen => resumen.Id == pendiente.Id);
        pendientes.ShouldNotContain(resumen => resumen.Id == activa.Id);
        pendientes.ShouldAllBe(resumen => resumen.AccountStatus == StaffAccountState.Pendiente);

        var cuentasActivas = await ProfesionalTestHelpers.ListAsync(admin, "accountStatus=Activa");
        cuentasActivas.ShouldContain(resumen => resumen.Id == activa.Id);
        cuentasActivas.ShouldNotContain(resumen => resumen.Id == pendiente.Id);
        cuentasActivas.ShouldAllBe(resumen => resumen.AccountStatus == StaffAccountState.Activa);

        var cuentasInactivas = await ProfesionalTestHelpers.ListAsync(admin, "accountStatus=Inactiva");
        cuentasInactivas.ShouldContain(resumen => resumen.Id == inactiva.Id);
        cuentasInactivas.ShouldAllBe(resumen => resumen.AccountStatus == StaffAccountState.Inactiva);

        // La combinación de filtros se aplica entera.
        var combinada = await ProfesionalTestHelpers.ListAsync(
            admin,
            $"{Query("especialidadId", ortodoncia.Id)}&{Query("localId", norte.Id)}&fichaActiva=true&accountStatus=Activa");
        combinada.ShouldHaveSingleItem().Id.ShouldBe(activa.Id);
        activa.StaffAccountId.ShouldBe(activaAccountId);
    }

    [Fact]
    public async Task Accounts_without_a_ficha_do_not_appear_in_the_list()
    {
        using var admin = await AdminAsync();
        var sinFicha = StaffTestHelpers.UniqueEmail("lista.sin.ficha");
        await factory.SeedAccountAsync(sinFicha, ProfesionalTestHelpers.NewPassword, StaffRole.Professional);

        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", sinFicha))).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_new_email_creates_the_ficha_the_pending_account_and_the_invitation()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("alta.endpoint");

        using var response = await ProfesionalTestHelpers.CreateAsync(
            admin,
            email,
            [especialidad.Id],
            [local.Id],
            "  COP 30  ",
            "Dra. Nueva");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ficha = await ProfesionalTestHelpers.ReadAsync(response);
        response.Headers.Location?.OriginalString.ShouldBe($"/api/profesionales/{ficha.Id}");

        ficha.ShouldSatisfyAllConditions(
            creada => creada.Id.ShouldNotBe(Guid.Empty),
            creada => creada.StaffAccountId.ShouldNotBe(Guid.Empty),
            creada => creada.StaffAccountId.ShouldNotBe(creada.Id),
            creada => creada.DisplayName.ShouldBe("Dra. Nueva"),
            creada => creada.Email.ShouldBe(email),
            creada => creada.Phone.ShouldBeNull(),
            creada => creada.Colegiatura.ShouldBe("COP 30"),
            creada => creada.IsActive.ShouldBeTrue(),
            creada => creada.AccountStatus.ShouldBe(StaffAccountState.Pendiente),
            creada => creada.InvitationFailed.ShouldBeFalse(),
            creada => creada.Especialidades.ShouldHaveSingleItem().Id.ShouldBe(especialidad.Id),
            creada => creada.Locales.ShouldHaveSingleItem().Id.ShouldBe(local.Id));

        ShouldBeSameFicha(ficha, await ProfesionalTestHelpers.GetAsync(admin, ficha.Id));
        factory.Emails.LastTokenTo(email).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task An_email_of_a_professional_account_without_ficha_is_linked_without_sending_email()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("alta.vinculo");
        var accountId = await factory.SeedAccountAsync(
            email,
            ProfesionalTestHelpers.NewPassword,
            StaffRole.Professional);

        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            email,
            [especialidad.Id],
            [local.Id]);

        ficha.StaffAccountId.ShouldBe(accountId);
        ficha.AccountStatus.ShouldBe(StaffAccountState.Activa);
        ficha.InvitationFailed.ShouldBeFalse();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("sin-correo", "Email")]
    [InlineData("correo-invalido", "Email")]
    [InlineData("correo-largo", "Email")]
    [InlineData("sin-nombre", "DisplayName")]
    [InlineData("nombre-largo", "DisplayName")]
    [InlineData("colegiatura-larga", "Colegiatura")]
    [InlineData("sin-especialidad", "EspecialidadIds")]
    [InlineData("especialidad-vacia", "EspecialidadIds")]
    [InlineData("sin-local", "LocalIds")]
    [InlineData("local-vacio", "LocalIds")]
    public async Task The_alta_rejects_an_invalid_form_with_a_field_error(string caso, string campo)
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail($"alta.{caso}");

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["displayName"] = "Profesional de pruebas",
            ["colegiatura"] = null,
            ["especialidadIds"] = new[] { especialidad.Id },
            ["localIds"] = new[] { local.Id },
        };

        switch (caso)
        {
            case "sin-correo":
                body.Remove("email");
                break;
            case "correo-invalido":
                body["email"] = "no-es-un-correo";
                break;
            case "correo-largo":
                body["email"] = $"{new string('c', 250)}@mousedent.test";
                break;
            case "sin-nombre":
                body.Remove("displayName");
                break;
            case "nombre-largo":
                body["displayName"] = new string('n', 121);
                break;
            case "colegiatura-larga":
                body["colegiatura"] = new string('C', 31);
                break;
            case "sin-especialidad":
                body.Remove("especialidadIds");
                break;
            case "especialidad-vacia":
                body["especialidadIds"] = new[] { Guid.Empty };
                break;
            case "sin-local":
                body.Remove("localIds");
                break;
            case "local-vacio":
                body["localIds"] = new[] { Guid.Empty };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caso), caso, "Caso de forma sin datos.");
        }

        using var response = await admin.PostAsJsonAsync("/api/profesionales", body, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationAsync(response)).Keys.ShouldContain(campo);
        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", email))).ShouldBeEmpty();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("especialidad-inactiva", "EspecialidadIds")]
    [InlineData("especialidad-inexistente", "EspecialidadIds")]
    [InlineData("local-inactivo", "LocalIds")]
    [InlineData("local-inexistente", "LocalIds")]
    public async Task The_alta_rejects_inactive_or_unknown_assignments(string caso, string campo)
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var inactiva = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin, isActive: false);
        var inactivo = await ProfesionalTestHelpers.CreateLocalAsync(admin, isActive: false);
        var email = StaffTestHelpers.UniqueEmail($"alta.{caso}");

        var especialidadIds = caso switch
        {
            "especialidad-inactiva" => new[] { inactiva.Id },
            "especialidad-inexistente" => new[] { Guid.NewGuid() },
            _ => new[] { especialidad.Id },
        };
        var localIds = caso switch
        {
            "local-inactivo" => new[] { inactivo.Id },
            "local-inexistente" => new[] { Guid.NewGuid() },
            _ => new[] { local.Id },
        };

        using var response = await ProfesionalTestHelpers.CreateAsync(admin, email, especialidadIds, localIds);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Solo el campo afectado: el otro llegó activo y no debe marcarse.
        (await ReadValidationAsync(response)).Keys.ShouldBe([campo]);
        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", email))).ShouldBeEmpty();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_alta_rejects_a_duplicate_colegiatura_without_creating_data()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var colegiatura = $"COP DUP {Guid.NewGuid():N}"[..20];
        await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("alta.colegiatura"),
            [especialidad.Id],
            [local.Id],
            colegiatura);

        var repetidaEmail = StaffTestHelpers.UniqueEmail("alta.colegiatura.repetida");
        using var response = await ProfesionalTestHelpers.CreateAsync(
            admin,
            repetidaEmail,
            [especialidad.Id],
            [local.Id],
            colegiatura.ToLowerInvariant());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var colegiaturaDuplicada = await ReadProblemAsync(response);
        colegiaturaDuplicada.Title.ShouldBe("Colegiatura duplicada");
        CodeOf(colegiaturaDuplicada).ShouldBe(ProfesionalErrorCodes.ColegiaturaDuplicada);
        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", repetidaEmail))).ShouldBeEmpty();
        factory.Emails.SentTo(repetidaEmail).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_alta_rejects_an_email_of_another_profile_or_of_an_account_with_a_ficha()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        await factory.SeedReceptionAccountAsync();

        using (var otroPerfil = await ProfesionalTestHelpers.CreateAsync(
            admin,
            MouseDentApiFactory.ReceptionEmail,
            [especialidad.Id],
            [local.Id]))
        {
            otroPerfil.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var otraCuenta = await ReadProblemAsync(otroPerfil);
            otraCuenta.Title.ShouldBe("Correo de otra cuenta");
            CodeOf(otraCuenta).ShouldBe(ProfesionalErrorCodes.CorreoDeOtraCuenta);
        }

        var email = StaffTestHelpers.UniqueEmail("alta.con.ficha");
        await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            email,
            [especialidad.Id],
            [local.Id]);

        using var segunda = await ProfesionalTestHelpers.CreateAsync(
            admin,
            email,
            [especialidad.Id],
            [local.Id]);

        segunda.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var conFicha = await ReadProblemAsync(segunda);
        conFicha.Title.ShouldBe("Correo con ficha");
        CodeOf(conFicha).ShouldBe(ProfesionalErrorCodes.CorreoConFicha);
        (await ProfesionalTestHelpers.ListAsync(admin, Query("search", email))).ShouldHaveSingleItem();
        factory.Emails.SentTo(email).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_failed_invitation_reports_the_ficha_identifier_and_the_resend_clears_it()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("alta.fallida");
        factory.Emails.FailingAddresses.Add(email);

        using var response = await ProfesionalTestHelpers.CreateAsync(admin, email, [especialidad.Id], [local.Id]);
        factory.Emails.FailingAddresses.Remove(email);

        // La ficha y la cuenta quedaron guardadas: el fallo del correo no es un error del alta.
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var problem = await ReadProblemAsync(response);
        problem.Title.ShouldBe("Invitación sin enviar");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
        CodeOf(problem).ShouldBe(ProfesionalErrorCodes.InvitacionSinEnviar);
        var profesionalId = problem.Extensions.ShouldNotBeNull()["profesionalId"].GetGuid();
        var ficha = await ProfesionalTestHelpers.GetAsync(admin, profesionalId);
        ficha.AccountStatus.ShouldBe(StaffAccountState.Pendiente);
        ficha.InvitationFailed.ShouldBeTrue();
        factory.Emails.SentTo(email).ShouldBeEmpty();

        // El reenvío que tampoco sale informa el fallo sin presentar la operación como
        // exitosa, y la ficha conserva la marca y el identificador.
        factory.Emails.FailingAddresses.Add(email);
        using (var reenvioFallido = await ProfesionalTestHelpers.ResendInvitationAsync(admin, profesionalId))
        {
            factory.Emails.FailingAddresses.Remove(email);

            reenvioFallido.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
            var problemaReenvio = await ReadProblemAsync(reenvioFallido);
            problemaReenvio.Title.ShouldBe("Invitación sin enviar");
            CodeOf(problemaReenvio).ShouldBe(ProfesionalErrorCodes.InvitacionSinEnviar);
            problemaReenvio.Extensions.ShouldNotBeNull()["profesionalId"].GetGuid().ShouldBe(profesionalId);
        }

        (await ProfesionalTestHelpers.GetAsync(admin, profesionalId)).InvitationFailed.ShouldBeTrue();

        using var resent = await ProfesionalTestHelpers.ResendInvitationAsync(admin, profesionalId);

        resent.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actualizada = await ProfesionalTestHelpers.ReadAsync(resent);
        actualizada.InvitationFailed.ShouldBeFalse();
        actualizada.AccountStatus.ShouldBe(StaffAccountState.Pendiente);
        factory.Emails.SentTo(email).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_missing_ficha_is_not_found_in_every_operation()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var id = Guid.NewGuid();

        using (var get = await admin.GetAsync($"/api/profesionales/{id}", Token))
        {
            get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await ReadProblemAsync(get)).Title.ShouldBe("Ficha no encontrada");
        }

        using (var put = await ProfesionalTestHelpers.UpdateAsync(admin, id, [especialidad.Id], [local.Id]))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var patch = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, id, isActive: true))
        {
            patch.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var post = await ProfesionalTestHelpers.ResendInvitationAsync(admin, id);

        post.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_edit_updates_the_ficha_and_keeps_the_account()
    {
        using var admin = await AdminAsync();
        var odontologia = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var ortodoncia = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var centro = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var norte = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var (_, ficha) = await ProfesionalTestHelpers.LinkedActiveProfesionalAsync(
            factory,
            admin,
            [odontologia.Id],
            [centro.Id],
            "COP EDICION 1");

        using var response = await ProfesionalTestHelpers.UpdateAsync(
            admin,
            ficha.Id,
            [ortodoncia.Id],
            [norte.Id],
            "  COP EDICION 2  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actualizada = await ProfesionalTestHelpers.ReadAsync(response);
        actualizada.ShouldSatisfyAllConditions(
            resumen => resumen.Id.ShouldBe(ficha.Id),
            resumen => resumen.StaffAccountId.ShouldBe(ficha.StaffAccountId),
            resumen => resumen.DisplayName.ShouldBe(ficha.DisplayName),
            resumen => resumen.Email.ShouldBe(ficha.Email),
            resumen => resumen.Phone.ShouldBe(ficha.Phone),
            resumen => resumen.Colegiatura.ShouldBe("COP EDICION 2"),
            resumen => resumen.IsActive.ShouldBeTrue(),
            resumen => resumen.Especialidades.ShouldHaveSingleItem().Id.ShouldBe(ortodoncia.Id),
            resumen => resumen.Locales.ShouldHaveSingleItem().Id.ShouldBe(norte.Id));

        var stored = await ProfesionalTestHelpers.GetAsync(admin, ficha.Id);
        stored.Colegiatura.ShouldBe("COP EDICION 2");
        stored.Especialidades.ShouldHaveSingleItem().Name.ShouldBe(ortodoncia.Name);
        stored.Locales.ShouldHaveSingleItem().Name.ShouldBe(norte.Name);
    }

    [Fact]
    public async Task The_edit_rejects_a_duplicate_colegiatura_and_keeps_the_ficha_as_it_was()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var colegiatura = $"COP REP {Guid.NewGuid():N}"[..16];
        await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("edicion.primera"),
            [especialidad.Id],
            [local.Id],
            colegiatura);
        var segunda = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("edicion.segunda"),
            [especialidad.Id],
            [local.Id]);

        using var response = await ProfesionalTestHelpers.UpdateAsync(
            admin,
            segunda.Id,
            [especialidad.Id],
            [local.Id],
            colegiatura.ToLowerInvariant());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var duplicada = await ReadProblemAsync(response);
        duplicada.Title.ShouldBe("Colegiatura duplicada");
        CodeOf(duplicada).ShouldBe(ProfesionalErrorCodes.ColegiaturaDuplicada);
        (await ProfesionalTestHelpers.GetAsync(admin, segunda.Id)).Colegiatura.ShouldBeNull();
    }

    [Fact]
    public async Task The_edit_of_an_active_ficha_keeps_the_minimum_and_the_inactive_one_accepts_inactive_assignments()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("edicion.minimo"),
            [especialidad.Id],
            [local.Id]);

        // Las asignaciones existentes se desactivan después: la ficha no puede quedarse
        // sin una especialidad activa ni sin un local activo mientras esté activa.
        await ProfesionalTestHelpers.SetEspecialidadStatusAsync(admin, especialidad.Id, isActive: false);
        await ProfesionalTestHelpers.SetLocalStatusAsync(admin, local.Id, isActive: false);

        using (var rechazada = await ProfesionalTestHelpers.UpdateAsync(
            admin,
            ficha.Id,
            [especialidad.Id],
            [local.Id]))
        {
            rechazada.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var errors = await ReadValidationAsync(rechazada);

            // Falta el mínimo de los dos campos, así que se marcan los dos.
            errors.Keys.ShouldBe(["EspecialidadIds", "LocalIds"], ignoreOrder: true);
        }

        var sinCambios = await ProfesionalTestHelpers.GetAsync(admin, ficha.Id);
        sinCambios.IsActive.ShouldBeTrue();
        sinCambios.Especialidades.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
        sinCambios.Locales.ShouldHaveSingleItem().IsActive.ShouldBeFalse();

        // Una ficha inactiva se edita conservando las asignaciones inactivas.
        using (var desactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: false))
        {
            desactivada.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ProfesionalTestHelpers.ReadAsync(desactivada)).IsActive.ShouldBeFalse();
        }

        using (var editada = await ProfesionalTestHelpers.UpdateAsync(
            admin,
            ficha.Id,
            [especialidad.Id],
            [local.Id],
            "COP INACTIVA"))
        {
            editada.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ProfesionalTestHelpers.ReadAsync(editada)).Colegiatura.ShouldBe("COP INACTIVA");
        }

        // Sin una especialidad activa y un local activo no se puede reactivar.
        using var reactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: true);

        reactivada.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var incompleta = await ReadProblemAsync(reactivada);
        incompleta.Title.ShouldBe("Activación incompleta");
        CodeOf(incompleta).ShouldBe(ProfesionalErrorCodes.ActivacionIncompleta);
        (await ProfesionalTestHelpers.GetAsync(admin, ficha.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task The_edit_of_an_inactive_ficha_can_empty_its_assignments_and_its_colegiatura()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("edicion.vacia"),
            [especialidad.Id],
            [local.Id],
            "COP VACIA");

        using (var desactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: false))
        {
            desactivada.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Para vaciar una ficha se desactiva primero: la inactiva acepta quedarse sin
        // especialidades, sin locales y sin colegiatura.
        using var response = await ProfesionalTestHelpers.UpdateAsync(admin, ficha.Id, [], []);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var vacia = await ProfesionalTestHelpers.ReadAsync(response);
        vacia.ShouldSatisfyAllConditions(
            guardada => guardada.IsActive.ShouldBeFalse(),
            guardada => guardada.Colegiatura.ShouldBeNull(),
            guardada => guardada.Especialidades.ShouldBeEmpty(),
            guardada => guardada.Locales.ShouldBeEmpty());

        var stored = await ProfesionalTestHelpers.GetAsync(admin, ficha.Id);
        stored.Colegiatura.ShouldBeNull();
        stored.Especialidades.ShouldBeEmpty();
        stored.Locales.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("especialidad-vacia", "EspecialidadIds")]
    [InlineData("local-vacio", "LocalIds")]
    public async Task The_edit_rejects_an_empty_assignment_identifier(string caso, string campo)
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail($"edicion.{caso}"),
            [especialidad.Id],
            [local.Id]);

        var especialidadIds = caso == "especialidad-vacia"
            ? new[] { especialidad.Id, Guid.Empty }
            : new[] { especialidad.Id };
        var localIds = caso == "local-vacio"
            ? new[] { local.Id, Guid.Empty }
            : new[] { local.Id };

        using var response = await ProfesionalTestHelpers.UpdateAsync(admin, ficha.Id, especialidadIds, localIds);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationAsync(response)).Keys.ShouldBe([campo]);
    }

    [Fact]
    public async Task The_edit_of_an_active_ficha_rejects_empty_assignments_as_an_incomplete_activation()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("edicion.activa.vacia"),
            [especialidad.Id],
            [local.Id]);
        var antes = await ProfesionalTestHelpers.GetAsync(admin, ficha.Id);

        using var response = await ProfesionalTestHelpers.UpdateAsync(admin, ficha.Id, [], []);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = await ReadValidationAsync(response);
        errors.Keys.ShouldBe(["EspecialidadIds", "LocalIds"], ignoreOrder: true);
        errors.Values.SelectMany(mensajes => mensajes)
            .ShouldAllBe(mensaje => mensaje.Contains("Desactívala antes", StringComparison.Ordinal));
        ShouldBeSameFicha(antes, await ProfesionalTestHelpers.GetAsync(admin, ficha.Id));
    }

    [Theory]
    [InlineData("especialidad-inactiva", "EspecialidadIds")]
    [InlineData("especialidad-inexistente", "EspecialidadIds")]
    [InlineData("local-inactivo", "LocalIds")]
    [InlineData("local-inexistente", "LocalIds")]
    public async Task The_edit_rejects_inactive_or_unknown_assignments_without_changing_the_ficha(
        string caso,
        string campo)
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var inactiva = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin, isActive: false);
        var inactivo = await ProfesionalTestHelpers.CreateLocalAsync(admin, isActive: false);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail($"edicion.{caso}"),
            [especialidad.Id],
            [local.Id]);
        var antes = await ProfesionalTestHelpers.GetAsync(admin, ficha.Id);

        var especialidadIds = caso switch
        {
            "especialidad-inactiva" => new[] { especialidad.Id, inactiva.Id },
            "especialidad-inexistente" => new[] { especialidad.Id, Guid.NewGuid() },
            _ => new[] { especialidad.Id },
        };
        var localIds = caso switch
        {
            "local-inactivo" => new[] { local.Id, inactivo.Id },
            "local-inexistente" => new[] { local.Id, Guid.NewGuid() },
            _ => new[] { local.Id },
        };

        using var response = await ProfesionalTestHelpers.UpdateAsync(admin, ficha.Id, especialidadIds, localIds);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationAsync(response)).Keys.ShouldBe([campo]);
        ShouldBeSameFicha(antes, await ProfesionalTestHelpers.GetAsync(admin, ficha.Id));
    }

    [Fact]
    public async Task The_status_endpoint_deactivates_and_reactivates_keeping_the_assignments()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("estado.ficha"),
            [especialidad.Id],
            [local.Id]);

        using (var desactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: false))
        {
            desactivada.StatusCode.ShouldBe(HttpStatusCode.OK);
            var estado = await ProfesionalTestHelpers.ReadAsync(desactivada);
            estado.IsActive.ShouldBeFalse();
            estado.AccountStatus.ShouldBe(StaffAccountState.Pendiente);
            estado.Especialidades.ShouldHaveSingleItem().Id.ShouldBe(especialidad.Id);
            estado.Locales.ShouldHaveSingleItem().Id.ShouldBe(local.Id);
        }

        using (var reactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: true))
        {
            reactivada.StatusCode.ShouldBe(HttpStatusCode.OK);
            var estado = await ProfesionalTestHelpers.ReadAsync(reactivada);
            estado.IsActive.ShouldBeTrue();
            estado.Especialidades.ShouldHaveSingleItem().Id.ShouldBe(especialidad.Id);
            estado.Locales.ShouldHaveSingleItem().Id.ShouldBe(local.Id);
        }

        // El estado es obligatorio: un cuerpo vacío no se interpreta como desactivación.
        using var sinEstado = await admin.PatchAsJsonAsync(
            $"/api/profesionales/{ficha.Id}/status",
            new { },
            Token);

        sinEstado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(sinEstado)).Title.ShouldNotBeNullOrWhiteSpace();
        (await ProfesionalTestHelpers.GetAsync(admin, ficha.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Repeating_the_current_status_is_idempotent_and_a_real_reactivation_still_requires_the_minimum()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("estado.repetido"),
            [especialidad.Id],
            [local.Id]);

        // Las asignaciones se desactivan después en el catálogo y en el local.
        await ProfesionalTestHelpers.SetEspecialidadStatusAsync(admin, especialidad.Id, isActive: false);
        await ProfesionalTestHelpers.SetLocalStatusAsync(admin, local.Id, isActive: false);

        // Pedir el estado que la ficha ya tiene no es una reactivación: no revalida el mínimo.
        using (var repetido = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: true))
        {
            repetido.StatusCode.ShouldBe(HttpStatusCode.OK);
            var estado = await ProfesionalTestHelpers.ReadAsync(repetido);
            estado.IsActive.ShouldBeTrue();
            estado.Especialidades.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
            estado.Locales.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
        }

        using (var desactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: false))
        {
            desactivada.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // La reactivación real sigue exigiendo una especialidad activa y un local activo.
        using var reactivada = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, ficha.Id, isActive: true);

        reactivada.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var incompleta = await ReadProblemAsync(reactivada);
        incompleta.Title.ShouldBe("Activación incompleta");
        CodeOf(incompleta).ShouldBe(ProfesionalErrorCodes.ActivacionIncompleta);
        (await ProfesionalTestHelpers.GetAsync(admin, ficha.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task The_invitation_is_rejected_when_the_account_has_a_password_or_is_inactive()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var (_, conPassword) = await ProfesionalTestHelpers.LinkedActiveProfesionalAsync(
            factory,
            admin,
            [especialidad.Id],
            [local.Id]);

        using (var conContrasena = await ProfesionalTestHelpers.ResendInvitationAsync(admin, conPassword.Id))
        {
            conContrasena.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await ReadProblemAsync(conContrasena)).Title.ShouldBe("Contraseña ya establecida");
        }

        var inactiva = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("invitacion.cuenta.inactiva"),
            [especialidad.Id],
            [local.Id]);
        await ProfesionalTestHelpers.SetAccountStatusAsync(admin, inactiva.StaffAccountId, isActive: false);

        using var cuentaInactiva = await ProfesionalTestHelpers.ResendInvitationAsync(admin, inactiva.Id);

        cuentaInactiva.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadProblemAsync(cuentaInactiva)).Title.ShouldBe("Cuenta inactiva");
    }

    private Task<HttpClient> AdminAsync() =>
        StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string method,
        string path,
        object? payload) => method switch
        {
            "GET" => client.GetAsync(path, Token),
            "POST" when payload is null => client.PostAsync(path, content: null, Token),
            "POST" => client.PostAsJsonAsync(path, payload, Token),
            "PUT" => client.PutAsJsonAsync(path, payload, Token),
            "PATCH" => client.PatchAsJsonAsync(path, payload, Token),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Método sin envío."),
        };

    private static string Query(string name, object value) =>
        $"{name}={Uri.EscapeDataString(value.ToString() ?? string.Empty)}";

    private static void ShouldBeSameFicha(ProfesionalResponse expected, ProfesionalResponse actual) =>
        actual.ShouldSatisfyAllConditions(
            ficha => ficha.Id.ShouldBe(expected.Id),
            ficha => ficha.StaffAccountId.ShouldBe(expected.StaffAccountId),
            ficha => ficha.DisplayName.ShouldBe(expected.DisplayName),
            ficha => ficha.Email.ShouldBe(expected.Email),
            ficha => ficha.Phone.ShouldBe(expected.Phone),
            ficha => ficha.Colegiatura.ShouldBe(expected.Colegiatura),
            ficha => ficha.IsActive.ShouldBe(expected.IsActive),
            ficha => ficha.AccountStatus.ShouldBe(expected.AccountStatus),
            ficha => ficha.InvitationFailed.ShouldBe(expected.InvitationFailed),
            ficha => ficha.Especialidades.Select(especialidad => (especialidad.Id, especialidad.Name, especialidad.IsActive))
                .ShouldBe(expected.Especialidades.Select(especialidad => (especialidad.Id, especialidad.Name, especialidad.IsActive))),
            ficha => ficha.Locales.Select(local => (local.Id, local.Name, local.IsActive))
                .ShouldBe(expected.Locales.Select(local => (local.Id, local.Name, local.IsActive))));

    private static async Task<ProblemPayload> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(Token);
        problem.ShouldNotBeNull();

        return problem;
    }

    /// <summary>Código estable publicado en la extensión <c>code</c> del <c>ProblemDetails</c>.</summary>
    private static string? CodeOf(ProblemPayload problem) =>
        problem.Extensions is not null && problem.Extensions.TryGetValue("code", out var code)
            ? code.GetString()
            : null;

    private static async Task<Dictionary<string, string[]>> ReadValidationAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(Token);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldNotBeNull();

        return problem.Errors;
    }

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }
    }

    private sealed record ValidationProblemPayload(Dictionary<string, string[]>? Errors);
}

/// <summary>
/// La cuenta que el alta leyó desaparece antes de la escritura. El producto no borra
/// cuentas y la ventana entre la lectura y la escritura no se puede provocar por HTTP,
/// así que el rechazo se fabrica en el almacén de altas. Comprueba el
/// <c>ProblemDetails</c> propio de esa causa, distinto del de un correo repetido.
/// </summary>
public sealed class ProfesionalAltaCuentaPerdidaTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_account_that_disappears_before_the_write_reports_its_own_problem()
    {
        using var factory = new MissingAccountAltaFactory();
        using var admin = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("alta.cuenta.perdida");
        await factory.SeedAccountAsync(email, ProfesionalTestHelpers.NewPassword, StaffRole.Professional);

        using var response = await ProfesionalTestHelpers.CreateAsync(admin, email, [especialidad.Id], [local.Id]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(Token);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Cuenta no encontrada");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
        problem.ErrorCode.ShouldBe(ProfesionalErrorCodes.CuentaNoEncontrada);
        (await ProfesionalTestHelpers.ListAsync(admin, $"search={Uri.EscapeDataString(email)}")).ShouldBeEmpty();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_alta_that_loses_the_email_race_reports_the_concurrent_alta_code()
    {
        using var factory = new DuplicateEmailAltaFactory();
        using var admin = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);

        using var response = await ProfesionalTestHelpers.CreateAsync(
            admin,
            StaffTestHelpers.UniqueEmail("alta.simultanea.codigo"),
            [especialidad.Id],
            [local.Id]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(Token);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Alta simultánea");
        problem.ErrorCode.ShouldBe(ProfesionalErrorCodes.AltaSimultanea);
    }

    /// <summary>Reemplaza la escritura compuesta por el rechazo del correo tomado por otra alta.</summary>
    private sealed class DuplicateEmailAltaFactory() : MouseDentApiFactoryBase(useFileDatabase: false)
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IAltasDeProfesionales>();
            services.AddScoped<IAltasDeProfesionales, DuplicateEmailAltaStore>();
        }
    }

    /// <summary>Finge que otra alta confirmó el correo entre la lectura y la escritura.</summary>
    private sealed class DuplicateEmailAltaStore : IAltasDeProfesionales
    {
        public Task<ResultadoEscrituraAlta> CrearConCuentaAsync(
            StaffAccount cuenta,
            Profesional ficha,
            CancellationToken cancellationToken) =>
            Task.FromResult(ResultadoEscrituraAlta.CorreoDuplicado);

        public Task<ResultadoEscrituraAlta> VincularCuentaAsync(
            Profesional ficha,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("La prueba crea una cuenta nueva.");
    }

    /// <summary>Reemplaza la escritura compuesta por el rechazo de la cuenta desaparecida.</summary>
    private sealed class MissingAccountAltaFactory() : MouseDentApiFactoryBase(useFileDatabase: false)
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IAltasDeProfesionales>();
            services.AddScoped<IAltasDeProfesionales, MissingAccountAltaStore>();
        }
    }

    private sealed class MissingAccountAltaStore : IAltasDeProfesionales
    {
        public Task<ResultadoEscrituraAlta> CrearConCuentaAsync(
            StaffAccount cuenta,
            Profesional ficha,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("La prueba vincula una cuenta existente.");

        public Task<ResultadoEscrituraAlta> VincularCuentaAsync(
            Profesional ficha,
            CancellationToken cancellationToken) =>
            Task.FromResult(ResultadoEscrituraAlta.CuentaNoEncontrada);
    }

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }

        /// <summary>Código estable de la extensión <c>code</c>, si el error lo publica.</summary>
        public string? ErrorCode =>
            Extensions is not null && Extensions.TryGetValue("code", out var code)
                ? code.GetString()
                : null;
    }
}
