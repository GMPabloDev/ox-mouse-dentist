using System.ComponentModel;

namespace MouseDent.Api.Profesionales;

/// <summary>
/// Alta de una ficha. Pide los datos de la cuenta que se crea o se vincula y los
/// datos propios de la ficha. El teléfono no forma parte del alta: la cuenta se
/// crea sin teléfono y se edita en el módulo de personal.
/// </summary>
public sealed class CreateProfesionalForm
{
    [Description("Correo de la cuenta. Único entre todas, incluidas las inactivas y pendientes. Si ya pertenece a una cuenta Profesional sin ficha, el alta la vincula.")]
    public string Email { get; set; } = string.Empty;

    [Description("Nombre visible de la persona, que se guarda en la cuenta.")]
    public string DisplayName { get; set; } = string.Empty;

    [Description("Número de colegiatura opcional, de hasta 30 caracteres tras quitar los espacios de los extremos. Único entre todas las fichas, incluidas las inactivas.")]
    public string? Colegiatura { get; set; }

    [Description("Al menos una especialidad activa del catálogo.")]
    public IReadOnlyList<Guid>? EspecialidadIds { get; set; }

    [Description("Al menos un local de atención activo.")]
    public IReadOnlyList<Guid>? LocalIds { get; set; }
}

/// <summary>
/// Edición de la ficha: colegiatura, especialidades y locales. El nombre, el
/// correo, el teléfono y el perfil viven en la cuenta y se editan en el módulo de
/// personal; si llegan en el cuerpo, no se modifican.
/// </summary>
public sealed class UpdateProfesionalForm
{
    [Description("Número de colegiatura opcional, de hasta 30 caracteres tras quitar los espacios de los extremos. Único entre todas las fichas, incluidas las inactivas.")]
    public string? Colegiatura { get; set; }

    [Description("Especialidades asignadas. Solo se pueden añadir activas; las ya asignadas se conservan aunque después se hayan desactivado. Una ficha activa necesita al menos una activa y para vaciarlas se desactiva la ficha primero.")]
    public IReadOnlyList<Guid>? EspecialidadIds { get; set; }

    [Description("Locales de atención asignados. Solo se pueden añadir activos; los ya asignados se conservan aunque después se hayan desactivado. Una ficha activa necesita al menos uno activo y para vaciarlos se desactiva la ficha primero.")]
    public IReadOnlyList<Guid>? LocalIds { get; set; }
}
