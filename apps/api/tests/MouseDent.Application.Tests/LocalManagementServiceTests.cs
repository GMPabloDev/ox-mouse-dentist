using MouseDent.Application.Locales;
using MouseDent.Domain.Locales;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class LocalManagementServiceTests
{
    private static readonly byte[] PhotoBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

    [Fact]
    public async Task Creating_a_local_that_only_differs_in_case_and_spaces_from_an_inactive_one_is_rejected()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.Deactivate();
        var locales = new FakeLocales(stored);
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.CreateAsync(
            CreateCommand("  sede CENTRO  "),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.DuplicateName);
        locales.Saved.ShouldBeEmpty();
        photos.Staged.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_name_comparison_keeps_the_enye_and_ignores_case_and_spaces()
    {
        var locales = new FakeLocales(ExistingLocal("Sede Ñuñoa"));
        var service = new LocalManagementService(locales, new FakePhotos());

        var outcome = await service.CreateAsync(
            CreateCommand("  SEDE ÑUÑOA  "),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.DuplicateName);
        locales.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_local_saves_the_photo_with_the_content_type_of_the_upload()
    {
        var locales = new FakeLocales();
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.CreateAsync(
            CreateCommand("Sede Norte", Photo("image/webp")),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.Saved);
        var local = outcome.Local.ShouldNotBeNull();
        local.Photo.ShouldNotBeNull();
        local.Photo.ContentType.ShouldBe("image/webp");
        local.Photo.FileName.ShouldBe(photos.Staged.Single());
        photos.Committed.ShouldBe([local.Photo.FileName]);
        photos.Discarded.ShouldBeEmpty();
        locales.Saved.ShouldBe([local]);
    }

    [Fact]
    public async Task Publication_failure_does_not_save_a_new_local()
    {
        var locales = new FakeLocales();
        var photos = new FakePhotos { CommitFailure = true };
        var service = new LocalManagementService(locales, photos);
        await Should.ThrowAsync<IOException>(() => service.CreateAsync(
            CreateCommand("Sede Nueva", Photo("image/png")), TestContext.Current.CancellationToken));
        locales.Locals.ShouldBeEmpty();
        photos.Discarded.ShouldBe(photos.Staged);
    }

    [Fact]
    public async Task A_failed_save_leaves_no_local_and_discards_the_new_photo()
    {
        var locales = new FakeLocales { SaveFailure = new InvalidOperationException("la base no responde") };
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var creating = async () => await service.CreateAsync(
            CreateCommand("Sede Norte", Photo("image/png")),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(creating);
        photos.Staged.Count.ShouldBe(1);
        photos.Discarded.ShouldBe(photos.Staged);
        photos.Committed.ShouldBe(photos.Staged);
        locales.Locals.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_taken_while_saving_discards_the_new_photo()
    {
        var locales = new FakeLocales { SaveResult = LocalSaveResult.DuplicateName };
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.CreateAsync(
            CreateCommand("Sede Norte", Photo("image/png")),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.DuplicateName);
        photos.Discarded.ShouldBe(photos.Staged);
        photos.Committed.ShouldBe(photos.Staged);
        locales.Locals.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_update_keeps_the_previous_photo()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto-anterior.png", "image/png");
        var locales = new FakeLocales(stored) { SaveFailure = new InvalidOperationException("la base no responde") };
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var updating = async () => await service.UpdateAsync(
            stored.Id,
            new UpdateLocalCommand(Details("Sede Centro"), LocalPhotoChange.Replace, Photo("image/jpeg")),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(updating);
        photos.Committed.ShouldBe(photos.Staged);
        photos.Deleted.ShouldBeEmpty();
        photos.Discarded.ShouldBe(photos.Staged);
    }

    [Fact]
    public async Task Replacing_the_photo_publishes_the_new_file_and_deletes_the_previous_one()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto-anterior.png", "image/png");
        var locales = new FakeLocales(stored);
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.UpdateAsync(
            stored.Id,
            new UpdateLocalCommand(Details("Sede Centro"), LocalPhotoChange.Replace, Photo("image/jpeg")),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.Saved);
        var local = outcome.Local.ShouldNotBeNull();
        photos.Committed.ShouldBe([local.Photo!.FileName]);
        photos.Deleted.ShouldBe(["foto-anterior.png"]);
        local.Photo.ContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task Removing_the_photo_after_saving_deletes_the_file_and_clears_it()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto-anterior.png", "image/png");
        var locales = new FakeLocales(stored);
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.UpdateAsync(
            stored.Id,
            new UpdateLocalCommand(Details("Sede Centro"), LocalPhotoChange.Remove, Photo: null),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.Saved);
        outcome.Local!.Photo.ShouldBeNull();
        photos.Deleted.ShouldBe(["foto-anterior.png"]);
        photos.Staged.ShouldBeEmpty();
    }

    [Fact]
    public async Task Keeping_the_photo_does_not_touch_the_stored_file()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto-anterior.png", "image/png");
        var locales = new FakeLocales(stored);
        var photos = new FakePhotos();
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.UpdateAsync(
            stored.Id,
            new UpdateLocalCommand(Details("Sede Actualizada"), LocalPhotoChange.Keep, Photo: null),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.Saved);
        outcome.Local!.Name.ShouldBe("Sede Actualizada");
        outcome.Local.Photo!.FileName.ShouldBe("foto-anterior.png");
        photos.Deleted.ShouldBeEmpty();
        photos.Committed.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_photo_that_cannot_be_deleted_does_not_revert_the_saved_change()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto-anterior.png", "image/png");
        var locales = new FakeLocales(stored);
        var photos = new FakePhotos { DeleteSucceeds = false };
        var service = new LocalManagementService(locales, photos);

        var outcome = await service.UpdateAsync(
            stored.Id,
            new UpdateLocalCommand(Details("Sede Centro"), LocalPhotoChange.Replace, Photo("image/png")),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.Saved);
        photos.Deleted.ShouldBe(["foto-anterior.png"]);
        locales.Saved.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Updating_an_unknown_local_reports_not_found()
    {
        var service = new LocalManagementService(new FakeLocales(), new FakePhotos());

        var outcome = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateLocalCommand(Details("Sede Centro"), LocalPhotoChange.Keep, Photo: null),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(LocalOutcomeStatus.NotFound);
    }

    [Fact]
    public async Task Deactivating_and_activating_keeps_the_data_and_the_photo()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto.png", "image/png");
        var locales = new FakeLocales(stored);
        var service = new LocalManagementService(locales, new FakePhotos());

        var deactivated = await service.SetStatusAsync(stored.Id, false, TestContext.Current.CancellationToken);

        deactivated.Local.ShouldNotBeNull();
        deactivated.Local.IsActive.ShouldBeFalse();

        var activated = await service.SetStatusAsync(stored.Id, true, TestContext.Current.CancellationToken);

        activated.Local.ShouldNotBeNull();
        activated.Local.IsActive.ShouldBeTrue();
        activated.Local.Name.ShouldBe("Sede Centro");
        activated.Local.Photo!.FileName.ShouldBe("foto.png");
    }

    [Fact]
    public async Task Reading_a_photo_returns_the_stored_content_type_and_bytes()
    {
        var stored = ExistingLocal("Sede Centro");
        stored.SetPhoto("foto.png", "image/png");
        var photos = new FakePhotos();
        photos.Publish("foto.png", PhotoBytes);
        var service = new LocalManagementService(new FakeLocales(stored), photos);

        var content = await service.OpenPhotoAsync(stored.Id, TestContext.Current.CancellationToken);

        content.ShouldNotBeNull();
        content.ContentType.ShouldBe("image/png");
        content.Content.ShouldBe(PhotoBytes);
    }

    [Fact]
    public async Task Reading_a_photo_of_a_local_without_photo_returns_nothing()
    {
        var stored = ExistingLocal("Sede Centro");
        var service = new LocalManagementService(new FakeLocales(stored), new FakePhotos());

        var content = await service.OpenPhotoAsync(stored.Id, TestContext.Current.CancellationToken);

        content.ShouldBeNull();
    }

    private static Local ExistingLocal(string name, bool isActive = true)
    {
        var local = Local.Create(name, "Av. Siempre Viva 123", "+51 999 888 777", Schedules.WeekFromNineToEighteen());

        if (!isActive)
        {
            local.Deactivate();
        }

        return local;
    }

    private static LocalDetails Details(string name) =>
        new(name, "Av. Siempre Viva 123", "+51 999 888 777", Schedules.WeekFromNineToEighteen());

    private static CreateLocalCommand CreateCommand(string name, LocalPhotoUpload? photo = null) =>
        new(Details(name), photo);

    private static LocalPhotoUpload Photo(string contentType) =>
        new(new MemoryStream(PhotoBytes), contentType);

    private sealed class FakeLocales(params Local[] stored) : ILocales
    {
        private readonly List<Local> _locals = [.. stored];

        public List<Local> Saved { get; } = [];

        public IReadOnlyList<Local> Locals => _locals;

        public Exception? SaveFailure { get; init; }

        public LocalSaveResult SaveResult { get; init; } = LocalSaveResult.Saved;

        public Task<IReadOnlyList<Local>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Local>>(_locals);

        public Task<Local?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_locals.FirstOrDefault(local => local.Id == id));

        public Task<bool> NameIsTakenAsync(
            string normalizedName,
            Guid? excludeLocalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_locals.Any(local =>
                local.NormalizedName == normalizedName
                && (excludeLocalId is null || local.Id != excludeLocalId)));

        public Task<LocalSaveResult> SaveAsync(Local local, CancellationToken cancellationToken)
        {
            if (SaveFailure is not null)
            {
                throw SaveFailure;
            }

            if (SaveResult == LocalSaveResult.Saved)
            {
                if (!_locals.Contains(local))
                {
                    _locals.Add(local);
                }

                Saved.Add(local);
            }

            return Task.FromResult(SaveResult);
        }
    }

    private sealed class FakePhotos : ILocalPhotos
    {
        private readonly Dictionary<string, byte[]> _published = [];

        public List<string> Staged { get; } = [];

        public List<string> Committed { get; } = [];

        public List<string> Discarded { get; } = [];

        public List<string> Deleted { get; } = [];

        public bool DeleteSucceeds { get; init; } = true;

        public bool CommitFailure { get; init; }

        public void Publish(string fileName, byte[] content) => _published[fileName] = content;

        public async Task<StagedLocalPhoto> StageAsync(
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            var fileName = $"foto-{Staged.Count + 1}{Extension(contentType)}";
            Staged.Add(fileName);
            _published[fileName] = buffer.ToArray();

            return new StagedLocalPhoto(fileName, contentType, $"{fileName}.pendiente");
        }

        public void Commit(StagedLocalPhoto staged)
        {
            if (CommitFailure) throw new IOException("publicación fallida");
            Committed.Add(staged.FileName);
        }

        public Task DiscardAsync(StagedLocalPhoto staged, CancellationToken cancellationToken)
        {
            Discarded.Add(staged.FileName);
            _published.Remove(staged.FileName);
            return Task.CompletedTask;
        }

        public Task<LocalPhotoContent?> OpenAsync(
            string fileName,
            string contentType,
            CancellationToken cancellationToken) =>
            Task.FromResult(_published.TryGetValue(fileName, out var content)
                ? new LocalPhotoContent(contentType, content)
                : null);

        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            Deleted.Add(fileName);
            return Task.FromResult(DeleteSucceeds);
        }

        private static string Extension(string contentType) => contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png",
        };
    }
}
