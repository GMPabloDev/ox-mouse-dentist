namespace MouseDent.Api.Profesionales;

/// <summary>
/// Códigos estables de los errores de profesionales. Viajan en la extensión
/// <c>code</c> del <c>ProblemDetails</c>, junto al título y el detalle en español
/// que ve la persona. La web clasifica por el código y usa el título solo como
/// respaldo, de modo que reescribir un mensaje no cambia la forma de corregir el
/// formulario. Los errores de validación por campo no llevan código: la web ya los
/// clasifica por el estado 400 y los mensajes de cada campo.
/// </summary>
public static class ProfesionalErrorCodes
{
    /// <summary>Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.</summary>
    public const string ColegiaturaDuplicada = "colegiatura_duplicada";

    /// <summary>El correo pertenece a una cuenta que ya tiene una ficha.</summary>
    public const string CorreoConFicha = "correo_con_ficha";

    /// <summary>El correo pertenece a una cuenta de administración o recepción.</summary>
    public const string CorreoDeOtraCuenta = "correo_de_otra_cuenta";

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    public const string ActivacionIncompleta = "activacion_incompleta";

    /// <summary>Otra alta simultánea registró el correo entre la lectura y la escritura.</summary>
    public const string AltaSimultanea = "alta_simultanea";

    /// <summary>La cuenta a vincular desapareció entre la lectura y la escritura.</summary>
    public const string CuentaNoEncontrada = "cuenta_no_encontrada";

    /// <summary>La ficha y la cuenta se guardaron, pero el correo de la invitación no salió.</summary>
    public const string InvitacionSinEnviar = "invitacion_sin_enviar";
}
