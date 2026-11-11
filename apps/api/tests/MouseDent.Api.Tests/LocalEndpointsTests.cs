using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Api.Locales;
using MouseDent.Application.Locales;
using MouseDent.Infrastructure.Locales;
using Shouldly;

namespace MouseDent.Api.Tests;

public sealed class LocalEndpointsTests : IClassFixture<MouseDentApiFactory>
{
    private const string DefaultAddress = "Av. Siempre Viva 742";
    private const string DefaultPhone = "+51 999 888 777";

    private static readonly string[] Days =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private readonly MouseDentApiFactory _factory;

    public LocalEndpointsTests(MouseDentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_requests_are_rejected_in_every_operation()
    {
        using var client = _factory.CreateClient();

        foreach (var (method, path) in Operations())
        {
            using var response = await client.SendAsync(
                new HttpRequestMessage(method, path),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    [Fact]
    public async Task Requests_from_another_profile_are_forbidden_in_every_operation()
    {
        await _factory.SeedReceptionAccountAsync();
        using var client = await SignInAsync(
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
    public async Task A_local_without_photo_is_created_and_listed_with_its_schedule()
    {
        using var client = await SignInAdminAsync();
        var name = UniqueName("Sede Centro");

        using var form = CreateForm(name);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadLocalAsync(response);
        created.Name.ShouldBe(name);
        created.Address.ShouldBe(DefaultAddress);
        created.Phone.ShouldBe(DefaultPhone);
        created.IsActive.ShouldBeTrue();
        created.Photo.ShouldBeNull();
        created.Schedule.Select(day => day.Day).ShouldBe(AllDays());
        created.Schedule.ShouldAllBe(day => !day.IsClosed);
        created.Schedule[0].OpensAt.ShouldBe("09:00");
        created.Schedule[0].ClosesAt.ShouldBe("18:00");
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldBe($"/api/locales/{created.Id}");

        var listed = await ListAsync(client);
        listed.ShouldContain(local => local.Id == created.Id && local.Name == name);

        using var photoResponse = await client.GetAsync(
            $"/api/locales/{created.Id}/photo",
            TestContext.Current.CancellationToken);

        photoResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public static TheoryData<string, string, byte[]> AcceptedPhotos => new()
    {
        { "image/jpeg", "foto.jpg", Convert.FromBase64String("/9j//gAPTGF2YzYzLjEuMTAyAP/bAEMACAQEBAQEBQUFBQUFBgYGBgYGBgYGBgYGBgcHBwgICAcHBwYGBwcICAgICQkJCAgICAkJCgoKDAwLCw4ODhERFP/EAEsAAQEAAAAAAAAAAAAAAAAAAAAHAQEAAAAAAAAAAAAAAAAAAAAAEAEAAAAAAAAAAAAAAAAAAAAAEQEAAAAAAAAAAAAAAAAAAAAA/8AAEQgAAQABAwESAAISAAMSAP/aAAwDAQACEQMRAD8Av4AP/9k=") },
        { "image/png", "foto.png", PngImage() },
        { "image/webp", "foto.webp", Convert.FromBase64String("UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAgA0JaQAA3AA/vuUAAA=") },
    };

    [Theory]
    [MemberData(nameof(AcceptedPhotos))]
    public async Task Accepted_photo_types_are_stored_and_served_with_their_content_type(
        string contentType,
        string fileName,
        byte[] bytes)
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm(
            UniqueName("Sede Foto"),
            photo: bytes,
            photoContentType: contentType,
            photoFileName: fileName);

        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadLocalAsync(response);
        created.Photo.ShouldNotBeNull();
        created.Photo.ContentType.ShouldBe(contentType);
        created.Photo.Version.ShouldNotBeNullOrWhiteSpace();

        using var photoResponse = await client.GetAsync(
            $"/api/locales/{created.Id}/photo",
            TestContext.Current.CancellationToken);

        photoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        photoResponse.Content.Headers.ContentType?.MediaType.ShouldBe(contentType);
        (await photoResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .ShouldBe(bytes);
    }

    [Theory]
    [InlineData("image/gif", "animacion.gif")]
    [InlineData("application/pdf", "documento.pdf")]
    public async Task A_photo_with_an_unsupported_type_is_rejected(string contentType, string fileName)
    {
        using var client = await SignInAdminAsync();
        var name = UniqueName("Sede Rechazada");

        using var form = CreateForm(name, photo: [0x00, 0x01, 0x02], photoContentType: contentType, photoFileName: fileName);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Photo");

        (await ListAsync(client)).ShouldNotContain(local => local.Name == name);
    }

    [Fact]
    public async Task A_photo_whose_bytes_do_not_match_its_type_is_rejected()
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm(
            UniqueName("Sede Falsa"),
            photo: Encoding.UTF8.GetBytes("esto no es una imagen"),
            photoContentType: "image/png");

        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Photo");
    }

    public static TheoryData<string, byte[]> TruncatedPhotos => new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF] },
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0] },
        { "image/webp", [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50] },
        { "image/webp", [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20, 0, 0, 0, 0] },
    };

    [Theory]
    [MemberData(nameof(TruncatedPhotos))]
    public async Task Truncated_photos_are_rejected(string contentType, byte[] bytes)
    {
        using var client = await SignInAdminAsync();
        using var form = CreateForm(UniqueName("Sede Truncada"), photo: bytes, photoContentType: contentType);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationProblemAsync(response)).Errors.ShouldNotBeNull().ShouldContainKey("Photo");
    }

    [Fact]
    public async Task A_photo_larger_than_five_megabytes_is_rejected()
    {
        using var client = await SignInAdminAsync();
        var bytes = new byte[(5 * 1024 * 1024) + 1];
        PngImage().CopyTo(bytes, 0);

        using var form = CreateForm(UniqueName("Sede Pesada"), photo: bytes, photoContentType: "image/png");
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors["Photo"].ShouldContain(message => message.Contains("5 MB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_required_data_returns_field_errors()
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm("   ", address: string.Empty, phone: string.Empty);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Name");
        problem.Errors.ShouldContainKey("Address");
        problem.Errors.ShouldContainKey("Phone");
    }

    [Theory]
    [InlineData("[null]", "Schedule")]
    public async Task Invalid_schedule_entries_return_field_errors(string schedule, string field)
    {
        using var client = await SignInAdminAsync();
        using var form = CreateForm(UniqueName("Sede Invalida"), schedule: schedule);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationProblemAsync(response)).Errors.ShouldNotBeNull().ShouldContainKey(field);
    }

    [Fact]
    public async Task Missing_day_is_rejected()
    {
        using var client = await SignInAdminAsync();
        var schedule = ScheduleJson().Replace("\"Day\":\"Monday\",", "", StringComparison.Ordinal);
        using var form = CreateForm(UniqueName("Sin Dia"), schedule: schedule);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationProblemAsync(response)).Errors.ShouldNotBeNull().ShouldContainKey("Schedule");
    }

    [Fact]
    public async Task Invalid_day_in_reordered_schedule_uses_general_schedule_error()
    {
        using var client = await SignInAdminAsync();
        var entries = JsonSerializer.Deserialize<JsonElement[]>(ScheduleJson())!;
        var schedule = "[" + string.Join(",", entries.Reverse().Select(entry => entry.GetRawText()))
            .Replace("\"Monday\"", "99", StringComparison.Ordinal) + "]";
        using var form = CreateForm(UniqueName("Dia Invalido"), schedule: schedule);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationProblemAsync(response)).Errors.ShouldNotBeNull().ShouldContainKey("Schedule");
    }

    [Fact]
    public async Task Failed_staging_does_not_delete_an_existing_pending_file()
    {
        _ = _factory.CreateClient();
        var photos = _factory.Services.GetRequiredService<FileSystemLocalPhotos>();
        var name = $"{Guid.NewGuid():N}.png";
        var path = Path.Combine(photos.RootPath, ".pending", name + ".part");
        await File.WriteAllBytesAsync(path, [4, 5, 6], TestContext.Current.CancellationToken);
        try
        {
            await using var content = new MemoryStream([1, 2, 3]);
            await Should.ThrowAsync<IOException>(() => photos.StageNamedAsync(content, "image/png", name, TestContext.Current.CancellationToken));
            (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).ShouldBe(new byte[] { 4, 5, 6 });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Failed_photo_publication_does_not_delete_existing_file()
    {
        _ = _factory.CreateClient();
        var photos = _factory.Services.GetRequiredService<FileSystemLocalPhotos>();
        await using var content = new MemoryStream([1, 2, 3]);
        var staged = await photos.StageAsync(content, "image/png", TestContext.Current.CancellationToken);
        var target = Path.Combine(photos.RootPath, staged.FileName);
        await File.WriteAllBytesAsync(target, [4, 5, 6], TestContext.Current.CancellationToken);
        Should.Throw<IOException>(() => photos.Commit(staged));
        await photos.DiscardAsync(staged, TestContext.Current.CancellationToken);
        (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)).ShouldBe(new byte[] { 4, 5, 6 });
    }

    [Fact]
    public async Task An_out_of_range_day_is_rejected_on_its_field()
    {
        using var client = await SignInAdminAsync();
        var schedule = ScheduleJson().Replace("\"Sunday\"", "99", StringComparison.Ordinal);
        using var form = CreateForm(UniqueName("Sede Dia Invalido"), schedule: schedule);
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadValidationProblemAsync(response)).Errors.ShouldNotBeNull().ShouldContainKey("Schedule");
    }

    [Fact]
    public async Task A_schedule_without_open_days_is_rejected()
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm(UniqueName("Sede Cerrada"), schedule: ScheduleJson(closedDays: Days));
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Schedule");
    }

    [Fact]
    public async Task A_day_that_closes_before_opening_is_rejected_on_its_field()
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm(
            UniqueName("Sede Invertida"),
            schedule: ScheduleJson(overrideDay: ("Monday", "18:00", "09:00")));

        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Schedule[0].ClosesAt");
    }

    [Fact]
    public async Task A_repeated_name_is_rejected_ignoring_case_spaces_and_inactive_state()
    {
        using var client = await SignInAdminAsync();
        var name = UniqueName("Sede Duplicada");

        using var first = CreateForm(name);
        using var firstResponse = await client.PostAsync("/api/locales", first, TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadLocalAsync(firstResponse);

        using var second = CreateForm($"  {name.ToLowerInvariant()}  ");
        using var secondResponse = await client.PostAsync("/api/locales", second, TestContext.Current.CancellationToken);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        secondResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var deactivate = await client.PatchAsJsonAsync(
            $"/api/locales/{created.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var third = CreateForm(name.ToUpperInvariant());
        using var thirdResponse = await client.PostAsync("/api/locales", third, TestContext.Current.CancellationToken);
        thirdResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await ListAsync(client)).Count(local => local.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ShouldBe(1);
    }

    [Fact]
    public async Task A_local_is_deactivated_and_reactivated_without_losing_data_or_photo()
    {
        using var client = await SignInAdminAsync();
        var bytes = PngImage();

        using var form = CreateForm(UniqueName("Sede Estado"), photo: bytes, photoContentType: "image/png");
        using var createResponse = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        var created = await ReadLocalAsync(createResponse);

        using var deactivateResponse = await client.PatchAsJsonAsync(
            $"/api/locales/{created.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);

        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var deactivated = await ReadLocalAsync(deactivateResponse);
        deactivated.IsActive.ShouldBeFalse();
        deactivated.Name.ShouldBe(created.Name);
        deactivated.Address.ShouldBe(created.Address);
        deactivated.Phone.ShouldBe(created.Phone);
        deactivated.Schedule.ShouldBe(created.Schedule);
        deactivated.Photo.ShouldBe(created.Photo);

        var listed = (await ListAsync(client)).Single(local => local.Id == created.Id);
        listed.IsActive.ShouldBeFalse();

        using var photoResponse = await client.GetAsync(
            $"/api/locales/{created.Id}/photo",
            TestContext.Current.CancellationToken);
        photoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var activateResponse = await client.PatchAsJsonAsync(
            $"/api/locales/{created.Id}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);

        activateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reactivated = await ReadLocalAsync(activateResponse);
        reactivated.IsActive.ShouldBeTrue();
        reactivated.Photo.ShouldBe(created.Photo);
        reactivated.Schedule.ShouldBe(created.Schedule);
    }

    [Fact]
    public async Task A_saved_local_is_read_back_in_a_later_request()
    {
        using var client = await SignInAdminAsync();
        var name = UniqueName("Sede Persistente");
        var bytes = PngImage();

        using var form = CreateForm(name, photo: bytes, photoContentType: "image/png");
        using var response = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        var created = await ReadLocalAsync(response);

        using var otherClient = await SignInAdminAsync();
        var loaded = (await ListAsync(otherClient)).Single(local => local.Id == created.Id);

        loaded.Name.ShouldBe(name);
        loaded.Address.ShouldBe(DefaultAddress);
        loaded.IsActive.ShouldBeTrue();
        loaded.Schedule.ShouldBe(created.Schedule);
        loaded.Photo.ShouldBe(created.Photo);
    }

    [Fact]
    public async Task Publication_failure_preserves_the_old_local_and_does_not_create_a_new_one()
    {
        FailingPhotos? photos = null;
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILocalPhotos>();
            services.AddSingleton<ILocalPhotos>(provider => photos = new FailingPhotos(provider.GetRequiredService<FileSystemLocalPhotos>()));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _factory.AccessTokenAsync(MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var name = UniqueName("Original");
        using var form = CreateForm(name, photo: PngImage(), photoContentType: "image/png");
        using var createdResponse = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        createdResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var original = await ReadLocalAsync(createdResponse);
        photos.ShouldNotBeNull().Fail = true;

        using var update = CreateForm(UniqueName("Cambiado"), photo: PngImage(2), photoAction: "replace");
        using var updateResponse = await client.PutAsync($"/api/locales/{original.Id}", update, TestContext.Current.CancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        updateResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var stored = (await ListAsync(client)).Single(local => local.Id == original.Id);
        stored.Name.ShouldBe(original.Name);
        stored.Photo.ShouldBe(original.Photo);
        using var photoResponse = await client.GetAsync($"/api/locales/{original.Id}/photo", TestContext.Current.CancellationToken);
        photoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await photoResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(PngImage());

        var newName = UniqueName("Fallido");
        using var newForm = CreateForm(newName, photo: PngImage());
        using var newResponse = await client.PostAsync("/api/locales", newForm, TestContext.Current.CancellationToken);
        newResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await ListAsync(client)).ShouldNotContain(local => local.Name == newName);
        _factory.PendingPhotoFiles().ShouldBeEmpty();
    }

    private sealed class FailingPhotos(FileSystemLocalPhotos inner) : ILocalPhotos
    {
        public bool Fail { get; set; }
        public Task<StagedLocalPhoto> StageAsync(Stream content, string contentType, CancellationToken cancellationToken) => inner.StageAsync(content, contentType, cancellationToken);
        public void Commit(StagedLocalPhoto staged)
        {
            if (Fail) throw new IOException("Fallo de publicación simulado");
            inner.Commit(staged);
        }
        public Task DiscardAsync(StagedLocalPhoto staged, CancellationToken cancellationToken) => inner.DiscardAsync(staged, cancellationToken);
        public Task<LocalPhotoContent?> OpenAsync(string fileName, string contentType, CancellationToken cancellationToken) => inner.OpenAsync(fileName, contentType, cancellationToken);
        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken) => inner.DeleteAsync(fileName, cancellationToken);
    }

    [Fact]
    public async Task A_failed_update_keeps_the_previous_version_and_leaves_no_new_file()
    {
        using var client = await SignInAdminAsync();
        var name = UniqueName("Sede Original");
        var originalBytes = PngImage();
        var otherBytes = PngImage(2);

        using var originalForm = CreateForm(name, photo: originalBytes, photoContentType: "image/png");
        using var originalResponse = await client.PostAsync("/api/locales", originalForm, TestContext.Current.CancellationToken);
        var original = await ReadLocalAsync(originalResponse);

        var otherName = UniqueName("Sede Ocupada");
        using var otherForm = CreateForm(otherName);
        using var otherResponse = await client.PostAsync("/api/locales", otherForm, TestContext.Current.CancellationToken);
        otherResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var publishedBefore = _factory.PublishedPhotoFiles().Count;

        using var updateForm = CreateForm(
            "  " + otherName.ToUpperInvariant() + " ",
            photo: PngImage(3),
            photoContentType: "image/png",
            photoAction: "replace");

        using var updateResponse = await client.PutAsync(
            $"/api/locales/{original.Id}",
            updateForm,
            TestContext.Current.CancellationToken);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var stored = (await ListAsync(client)).Single(local => local.Id == original.Id);
        stored.Name.ShouldBe(name);
        stored.Photo.ShouldBe(original.Photo);

        using var photoResponse = await client.GetAsync(
            $"/api/locales/{original.Id}/photo",
            TestContext.Current.CancellationToken);
        (await photoResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .ShouldBe(originalBytes);

        _factory.PublishedPhotoFiles().Count.ShouldBe(publishedBefore);
        _factory.PendingPhotoFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_a_local_replaces_keeps_and_removes_the_photo()
    {
        using var client = await SignInAdminAsync();
        var first = PngImage();
        var second = PngImage(9);

        using var form = CreateForm(UniqueName("Sede Foto Variable"), photo: first, photoContentType: "image/png");
        using var createResponse = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        var created = await ReadLocalAsync(createResponse);
        var firstVersion = created.Photo!.Version;

        using var replaceForm = CreateForm(
            created.Name,
            snapshot: ScheduleSnapshot(created),
            photo: second,
            photoContentType: "image/png",
            photoAction: "replace");

        using var replaceResponse = await client.PutAsync(
            $"/api/locales/{created.Id}",
            replaceForm,
            TestContext.Current.CancellationToken);

        replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var replaced = await ReadLocalAsync(replaceResponse);
        replaced.Photo.ShouldNotBeNull();
        replaced.Photo.Version.ShouldNotBe(firstVersion);

        using var replacedPhoto = await client.GetAsync(
            $"/api/locales/{created.Id}/photo",
            TestContext.Current.CancellationToken);
        (await replacedPhoto.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .ShouldBe(second);
        _factory.PublishedPhotoFiles().ShouldNotContain(firstVersion);

        using var keepForm = CreateForm(created.Name, snapshot: ScheduleSnapshot(created), photoAction: "keep");
        using var keepResponse = await client.PutAsync(
            $"/api/locales/{created.Id}",
            keepForm,
            TestContext.Current.CancellationToken);

        keepResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadLocalAsync(keepResponse)).Photo.ShouldBe(replaced.Photo);

        using var removeForm = CreateForm(created.Name, snapshot: ScheduleSnapshot(created), photoAction: "remove");
        using var removeResponse = await client.PutAsync(
            $"/api/locales/{created.Id}",
            removeForm,
            TestContext.Current.CancellationToken);

        removeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadLocalAsync(removeResponse)).Photo.ShouldBeNull();

        using var missingPhoto = await client.GetAsync(
            $"/api/locales/{created.Id}/photo",
            TestContext.Current.CancellationToken);
        missingPhoto.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _factory.PublishedPhotoFiles().ShouldNotContain(replaced.Photo!.Version);
    }

    [Fact]
    public async Task Updating_without_a_photo_indication_is_rejected()
    {
        using var client = await SignInAdminAsync();

        using var form = CreateForm(UniqueName("Sede Sin Indicacion"));
        using var createResponse = await client.PostAsync("/api/locales", form, TestContext.Current.CancellationToken);
        var created = await ReadLocalAsync(createResponse);

        using var updateForm = CreateForm(created.Name);
        using var updateResponse = await client.PutAsync(
            $"/api/locales/{created.Id}",
            updateForm,
            TestContext.Current.CancellationToken);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(updateResponse);
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("PhotoAction");
    }

    [Fact]
    public async Task Operations_on_an_unknown_local_return_not_found()
    {
        using var client = await SignInAdminAsync();
        var id = Guid.NewGuid();

        using var updateForm = CreateForm("Sede Inexistente", photoAction: "keep");
        using var updateResponse = await client.PutAsync(
            $"/api/locales/{id}",
            updateForm,
            TestContext.Current.CancellationToken);

        using var statusResponse = await client.PatchAsJsonAsync(
            $"/api/locales/{id}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);

        using var photoResponse = await client.GetAsync(
            $"/api/locales/{id}/photo",
            TestContext.Current.CancellationToken);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        photoResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static IEnumerable<(HttpMethod Method, string Path)> Operations()
    {
        var id = Guid.NewGuid();

        yield return (HttpMethod.Get, "/api/locales");
        yield return (HttpMethod.Post, "/api/locales");
        yield return (HttpMethod.Put, $"/api/locales/{id}");
        yield return (HttpMethod.Patch, $"/api/locales/{id}/status");
        yield return (HttpMethod.Get, $"/api/locales/{id}/photo");
    }

    private async Task<HttpClient> SignInAdminAsync() =>
        await SignInAsync(MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);

    private async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _factory.AccessTokenAsync(email, password));

        return client;
    }

    private static async Task<IReadOnlyList<LocalResponse>> ListAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/locales", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var locals = await response.Content.ReadFromJsonAsync<LocalResponse[]>(TestContext.Current.CancellationToken);
        locals.ShouldNotBeNull();

        return locals;
    }

    private static async Task<LocalResponse> ReadLocalAsync(HttpResponseMessage response)
    {
        var local = await response.Content.ReadFromJsonAsync<LocalResponse>(TestContext.Current.CancellationToken);
        local.ShouldNotBeNull();

        return local;
    }

    private static async Task<ValidationProblemPayload> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(
            TestContext.Current.CancellationToken);

        problem.ShouldNotBeNull();
        return problem;
    }

    private static MultipartFormDataContent CreateForm(
        string name,
        string? address = null,
        string? phone = null,
        string? schedule = null,
        string? snapshot = null,
        byte[]? photo = null,
        string photoContentType = "image/png",
        string photoFileName = "foto.png",
        string? photoAction = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "name" },
            { new StringContent(address ?? DefaultAddress), "address" },
            { new StringContent(phone ?? DefaultPhone), "phone" },
            { new StringContent(schedule ?? snapshot ?? ScheduleJson()), "schedule" },
        };

        if (photoAction is not null)
        {
            form.Add(new StringContent(photoAction), "photoAction");
        }

        if (photo is not null)
        {
            var file = new ByteArrayContent(photo);
            file.Headers.ContentType = new MediaTypeHeaderValue(photoContentType);
            form.Add(file, "photo", photoFileName);
        }

        return form;
    }

    private static string ScheduleSnapshot(LocalResponse local) => JsonSerializer.Serialize(local.Schedule);

    private static string ScheduleJson(
        string opensAt = "09:00",
        string closesAt = "18:00",
        string[]? closedDays = null,
        (string Day, string OpensAt, string ClosesAt)? overrideDay = null)
    {
        var week = Days.Select(day =>
        {
            if (closedDays?.Contains(day) == true)
            {
                return new ScheduleEntry(day, true, null, null);
            }

            if (overrideDay is { } custom && custom.Day == day)
            {
                return new ScheduleEntry(day, false, custom.OpensAt, custom.ClosesAt);
            }

            return new ScheduleEntry(day, false, opensAt, closesAt);
        });

        return JsonSerializer.Serialize(week);
    }

    private static byte[] PngImage(byte marker = 0x61) => Convert.FromBase64String(marker switch
    {
        2 => "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNgYmAAAAAKAANQonMOAAAAAElFTkSuQmCC",
        3 => "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNgZmAAAAANAARPw/7SAAAAAElFTkSuQmCC",
        9 => "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGPgZGAAAAAfAAqTqaP5AAAAAElFTkSuQmCC",
        _ => "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNIZGAAAAEnAGIdeLkqAAAAAElFTkSuQmCC",
    });

    private static LocalWeekDay[] AllDays() =>
    [
        LocalWeekDay.Monday,
        LocalWeekDay.Tuesday,
        LocalWeekDay.Wednesday,
        LocalWeekDay.Thursday,
        LocalWeekDay.Friday,
        LocalWeekDay.Saturday,
        LocalWeekDay.Sunday,
    ];

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private sealed record ValidationProblemPayload(Dictionary<string, string[]>? Errors);

    private sealed record ScheduleEntry(string Day, bool IsClosed, string? OpensAt, string? ClosesAt);
}
