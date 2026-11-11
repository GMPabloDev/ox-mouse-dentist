using System.ComponentModel;

namespace MouseDent.Api.Locales;

/// <summary>
/// Campos comunes del alta y la edición de un local. La foto viaja como parte
/// opcional del formulario multipart.
/// </summary>
public class LocalDataForm
{
    [Description("Nombre del local. Único entre todos, incluidos los inactivos, sin distinguir mayúsculas ni espacios de los extremos.")]
    public string Name { get; set; } = string.Empty;

    [Description("Dirección del local. No se exige un formato concreto.")]
    public string Address { get; set; } = string.Empty;

    [Description("Teléfono del local. No se exige un formato concreto.")]
    public string Phone { get; set; } = string.Empty;

    [Description("Horario semanal en JSON: arreglo de los siete días LocalScheduleDay, con las horas en formato HH:mm y los días cerrados marcados con isClosed.")]
    public string Schedule { get; set; } = string.Empty;

    [Description("Foto opcional: JPG, PNG o WebP de hasta 5 MB.")]
    public IFormFile? Photo { get; set; }
}

public sealed class CreateLocalForm : LocalDataForm;

public sealed class UpdateLocalForm : LocalDataForm
{
    [Description("keep conserva la foto actual, replace la sustituye por el archivo enviado y remove la quita.")]
    public string PhotoAction { get; set; } = string.Empty;
}
