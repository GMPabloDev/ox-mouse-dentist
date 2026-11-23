namespace MouseDent.Domain.Auth;

/// <summary>
/// Estado visible de una cuenta del personal, derivado de su habilitación y de si
/// ya estableció su contraseña. Una cuenta desactivada es inactiva aunque nunca
/// haya establecido contraseña: la desactivación es una decisión explícita y
/// prevalece sobre la falta de contraseña.
/// </summary>
public enum EstadoDeCuenta
{
    /// <summary>Activa, a la espera de establecer la contraseña desde la invitación.</summary>
    Pendiente,

    /// <summary>Activa y con contraseña establecida.</summary>
    Activa,

    /// <summary>Desactivada. Conserva sus datos y no inicia sesión.</summary>
    Inactiva,
}
