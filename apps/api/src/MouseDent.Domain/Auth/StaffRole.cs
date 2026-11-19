namespace MouseDent.Domain.Auth;

/// <summary>
/// Perfiles del personal. Los nombres son identificadores estables: aparecen en el
/// claim <c>role</c> del token y en las políticas de autorización. Cada cuenta tiene
/// exactamente uno. El alcance por local se define en H-06, no aquí.
/// </summary>
public static class StaffRole
{
    public const string Admin = "Administrador";
    public const string Reception = "Recepcion";
    public const string Professional = "Profesional";

    public static readonly IReadOnlyList<string> All = [Admin, Reception, Professional];

    /// <summary>Indica si el valor es uno de los perfiles admitidos.</summary>
    public static bool IsKnown(string? profile) => profile is not null && All.Contains(profile);
}
