using MouseDent.Domain.Locales;

namespace MouseDent.Application.Locales;

/// <summary>
/// Caso de uso de gestión de locales. Coordina el almacén de locales con el de
/// fotos: la foto nueva se publica antes de guardar el local y la
/// foto anterior solo se borra después, de modo que un fallo conserva la
/// versión previamente guardada.
/// </summary>
public sealed class LocalManagementService(ILocales locales, ILocalPhotos photos)
{
    public Task<IReadOnlyList<Local>> ListAsync(CancellationToken cancellationToken) =>
        locales.ListAsync(cancellationToken);

    public async Task<LocalOutcome> CreateAsync(
        CreateLocalCommand command,
        CancellationToken cancellationToken)
    {
        if (await locales.NameIsTakenAsync(
                LocalNames.Normalize(command.Details.Name),
                excludeLocalId: null,
                cancellationToken))
        {
            return LocalOutcome.DuplicateName();
        }

        var local = Local.Create(
            command.Details.Name,
            command.Details.Address,
            command.Details.Phone,
            command.Details.Schedule);

        var staged = command.Photo is null
            ? null
            : await photos.StageAsync(command.Photo.Content, command.Photo.ContentType, cancellationToken);

        if (staged is not null)
        {
            local.SetPhoto(staged.FileName, staged.ContentType);
        }

        if (staged is not null)
        {
            try
            {
                photos.Commit(staged);
            }
            catch
            {
                await DiscardAsync(staged, CancellationToken.None);
                throw;
            }
        }

        if (!await SaveAsync(local, staged, cancellationToken))
        {
            return LocalOutcome.DuplicateName();
        }

        return LocalOutcome.Saved(local);
    }

    public async Task<LocalOutcome> UpdateAsync(
        Guid id,
        UpdateLocalCommand command,
        CancellationToken cancellationToken)
    {
        var local = await locales.FindAsync(id, cancellationToken);
        if (local is null)
        {
            return LocalOutcome.NotFound();
        }

        if (await locales.NameIsTakenAsync(
                LocalNames.Normalize(command.Details.Name),
                excludeLocalId: id,
                cancellationToken))
        {
            return LocalOutcome.DuplicateName();
        }

        StagedLocalPhoto? staged = null;
        if (command.PhotoChange == LocalPhotoChange.Replace)
        {
            if (command.Photo is null)
            {
                throw new InvalidOperationException("Sustituir la foto requiere un archivo nuevo.");
            }

            staged = await photos.StageAsync(command.Photo.Content, command.Photo.ContentType, cancellationToken);
        }

        var previousPhoto = local.Photo;
        if (staged is not null)
        {
            try
            {
                photos.Commit(staged);
            }
            catch
            {
                await DiscardAsync(staged, CancellationToken.None);
                throw;
            }
        }

        local.UpdateDetails(
            command.Details.Name,
            command.Details.Address,
            command.Details.Phone,
            command.Details.Schedule);

        switch (command.PhotoChange)
        {
            case LocalPhotoChange.Replace:
                local.SetPhoto(staged!.FileName, staged.ContentType);
                break;
            case LocalPhotoChange.Remove:
                local.RemovePhoto();
                break;
            case LocalPhotoChange.Keep:
                break;
        }

        if (!await SaveAsync(local, staged, cancellationToken))
        {
            return LocalOutcome.DuplicateName();
        }

        if (previousPhoto is not null && command.PhotoChange != LocalPhotoChange.Keep)
        {
            await photos.DeleteAsync(previousPhoto.FileName, cancellationToken);
        }

        return LocalOutcome.Saved(local);
    }

    public async Task<LocalOutcome> SetStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var local = await locales.FindAsync(id, cancellationToken);
        if (local is null)
        {
            return LocalOutcome.NotFound();
        }

        if (isActive)
        {
            local.Activate();
        }
        else
        {
            local.Deactivate();
        }

        await locales.SaveAsync(local, cancellationToken);
        return LocalOutcome.Saved(local);
    }

    public async Task<LocalPhotoContent?> OpenPhotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var local = await locales.FindAsync(id, cancellationToken);
        if (local?.Photo is not { } photo)
        {
            return null;
        }

        return await photos.OpenAsync(photo.FileName, photo.ContentType, cancellationToken);
    }

    /// <summary>
    /// Guarda el local y descarta el pendiente si algo falla. Devuelve
    /// <c>false</c> cuando el nombre choca con el índice único.
    /// </summary>
    private async Task<bool> SaveAsync(
        Local local,
        StagedLocalPhoto? staged,
        CancellationToken cancellationToken)
    {
        LocalSaveResult result;
        try
        {
            result = await locales.SaveAsync(local, cancellationToken);
        }
        catch
        {
            await DiscardAsync(staged, CancellationToken.None);
            throw;
        }

        if (result == LocalSaveResult.Saved)
        {
            return true;
        }

        await DiscardAsync(staged, CancellationToken.None);
        return false;
    }

    private async Task DiscardAsync(StagedLocalPhoto? staged, CancellationToken cancellationToken)
    {
        if (staged is not null)
        {
            await photos.DiscardAsync(staged, CancellationToken.None);
        }
    }
}
