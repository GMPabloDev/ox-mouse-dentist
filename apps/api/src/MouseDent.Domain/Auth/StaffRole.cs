namespace MouseDent.Domain.Auth;

/// <summary>
/// Perfiles del personal. Los nombres son identificadores estables: aparecen en el
/// claim <c>role</c> del token y en las políticas de autorización.
/// El alcance por local se define en H-06, no aquí.
/// </summary>
public static class StaffRole
{
    public const string Admin = "Administrador";
    public const string Reception = "Recepcion";
    public const string Professional = "Profesional";

    public static readonly IReadOnlyList<string> All = [Admin, Reception, Professional];
}
