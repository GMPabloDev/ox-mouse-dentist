using MouseDent.Domain.Locales;

namespace MouseDent.Domain.Auth;

/// <summary>
/// Regla de alcance por local. Administración opera en todos los locales por su
/// perfil. Recepción opera solo en sus locales asignados que estén activos: la
/// asignación a un local inactivo se conserva y no autoriza nada hasta que el
/// local vuelva a estar activo. Los locales de atención de un profesional se
/// definen en H-03 y su acceso a la agenda en H-09.
/// </summary>
public static class LocalAccess
{
    /// <summary>
    /// Indica si la cuenta puede operar en el local. Una cuenta inactiva o
    /// pendiente no opera en ningún local porque tampoco puede tener una sesión
    /// vigente.
    /// </summary>
    public static bool CanOperate(StaffAccount account, Local local)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(local);

        if (!account.IsActive || account.IsPending)
        {
            return false;
        }

        return account.Profile switch
        {
            StaffRole.Admin => true,
            StaffRole.Reception => local.IsActive && account.AssignedLocalIds.Contains(local.Id),
            _ => false,
        };
    }

    /// <summary>Locales donde la cuenta puede operar hoy.</summary>
    public static IReadOnlyList<Local> AccessibleLocals(
        StaffAccount account,
        IEnumerable<Local> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);

        return [.. locales.Where(local => CanOperate(account, local))];
    }
}
