using MouseDent.Domain.Locales;

namespace MouseDent.Application.Locales;

/// <summary>Datos del local que se guardan juntos: nombre, contacto y horario.</summary>
public sealed record LocalDetails(string Name, string Address, string Phone, WeeklySchedule Schedule);

/// <summary>Foto recibida en el borde, lista para escribirse en el almacenamiento.</summary>
public sealed record LocalPhotoUpload(Stream Content, string ContentType);

public sealed record CreateLocalCommand(LocalDetails Details, LocalPhotoUpload? Photo);

public enum LocalPhotoChange
{
    Keep,
    Replace,
    Remove,
}

public sealed record UpdateLocalCommand(
    LocalDetails Details,
    LocalPhotoChange PhotoChange,
    LocalPhotoUpload? Photo);
