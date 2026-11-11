namespace MouseDent.Domain.Locales;

/// <summary>
/// Local de atención. Desactivarlo conserva todos los datos: esta entrega no
/// tiene borrado definitivo. La foto se registra con el nombre de archivo que
/// genera el servidor y el tipo de contenido aceptado al subirla.
/// </summary>
public sealed class Local
{
    private Local()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Clave de unicidad del nombre, sin espacios en los extremos y en mayúsculas.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public WeeklySchedule Schedule { get; private set; } = null!;

    public string? PhotoFileName { get; private set; }

    public string? PhotoContentType { get; private set; }

    /// <summary>Foto guardada del local, o <c>null</c> cuando no tiene.</summary>
    public LocalPhoto? Photo =>
        PhotoFileName is null || PhotoContentType is null
            ? null
            : new LocalPhoto(PhotoFileName, PhotoContentType);

    public static Local Create(string name, string address, string phone, WeeklySchedule schedule)
    {
        var local = new Local
        {
            Id = Guid.NewGuid(),
            Address = address,
            Phone = phone,
            Schedule = schedule,
            IsActive = true,
        };

        local.Rename(name);
        return local;
    }

    public void UpdateDetails(string name, string address, string phone, WeeklySchedule schedule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(schedule);

        Rename(name);
        Address = address;
        Phone = phone;
        Schedule = schedule;
    }

    public void SetPhoto(string fileName, string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        PhotoFileName = fileName;
        PhotoContentType = contentType;
    }

    public void RemovePhoto()
    {
        PhotoFileName = null;
        PhotoContentType = null;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        NormalizedName = LocalNames.Normalize(name);
    }
}

/// <summary>Archivo de foto del local y el tipo de contenido con el que se sirve.</summary>
public sealed record LocalPhoto(string FileName, string ContentType);
