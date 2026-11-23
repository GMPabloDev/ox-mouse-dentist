using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Profesionales;

/// <summary>
/// Escritura compuesta de cuenta y ficha. El alta necesita crear o vincular la
/// cuenta y guardar la ficha en una sola transacción: <see cref="Auth.IStaffAccounts"/>
/// e <see cref="IProfesionales"/> confirman por su cuenta, así que encadenarlos
/// dejaría la cuenta creada cuando la ficha se rechaza.
/// </summary>
public interface IAltasDeProfesionales
{
    /// <summary>
    /// Inserta la cuenta pendiente y la ficha en una sola transacción. Un rechazo no
    /// deja cuenta ni ficha.
    /// </summary>
    Task<ResultadoEscrituraAlta> CrearConCuentaAsync(
        StaffAccount cuenta,
        Profesional ficha,
        CancellationToken cancellationToken);

    /// <summary>
    /// Vincula una cuenta existente con perfil <c>Profesional</c> y sin ficha. No
    /// modifica la cuenta: ni su nombre, ni su correo, ni su estado, ni su
    /// contraseña, ni la versión de su sesión. Un rechazo no deja ficha.
    /// </summary>
    Task<ResultadoEscrituraAlta> VincularCuentaAsync(
        Profesional ficha,
        CancellationToken cancellationToken);
}

/// <summary>Resultado de la escritura compuesta. Solo <see cref="Guardado"/> deja datos.</summary>
public enum ResultadoEscrituraAlta
{
    Guardado,

    /// <summary>Ya existe una cuenta con ese correo.</summary>
    CorreoDuplicado,

    /// <summary>La cuenta a vincular ya no existe.</summary>
    CuentaNoEncontrada,

    /// <summary>La cuenta a vincular no tiene perfil <c>Profesional</c>.</summary>
    PerfilIncorrecto,

    /// <summary>La cuenta a vincular ya tiene una ficha.</summary>
    CuentaYaVinculada,

    /// <summary>Otra ficha, incluso inactiva, tiene el mismo número de colegiatura.</summary>
    ColegiaturaDuplicada,

    /// <summary>La especialidad asignada está inactiva o no existe.</summary>
    EspecialidadInactiva,

    /// <summary>El local asignado está inactivo o no existe.</summary>
    LocalInactivo,

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    ActivacionIncompleta,
}
