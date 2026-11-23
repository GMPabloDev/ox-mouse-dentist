using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Domain.Auth;

/// <summary>
/// Regla de alcance por local. Administración opera en todos los locales por su
/// perfil. Recepción opera solo en sus locales asignados que estén activos: la
/// asignación a un local inactivo se conserva y no autoriza nada hasta que el
/// local vuelva a estar activo. Un profesional opera solo en los locales de
/// atención de su ficha activa, que además exige cuenta activa y con contraseña
/// establecida. La ficha se pasa siempre a la regla: omitirla dejaría al
/// profesional sin la comprobación que lo limita a sus locales.
/// </summary>
public static class LocalAccess
{
    /// <summary>
    /// Indica si la cuenta puede operar en el local. Una cuenta inactiva o
    /// pendiente no opera en ningún local porque tampoco puede tener una sesión
    /// vigente.
    /// </summary>
    /// <param name="ficha">
    /// Ficha vinculada a la cuenta. Administración y recepción pasan <c>null</c>
    /// porque no tienen ficha. Un profesional sin ficha también pasa <c>null</c>, y
    /// en ese caso no opera en ningún local. No tiene valor por defecto para que
    /// ningún llamador pueda omitir la comprobación al comprobar a un profesional.
    /// </param>
    public static bool CanOperate(StaffAccount account, Profesional? ficha, Local local)
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
            StaffRole.Professional => CanOperateAsProfessional(account, ficha, local),
            _ => false,
        };
    }

    /// <summary>Locales donde la cuenta puede operar hoy.</summary>
    public static IReadOnlyList<Local> AccessibleLocals(
        StaffAccount account,
        Profesional? ficha,
        IEnumerable<Local> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);

        return [.. locales.Where(local => CanOperate(account, ficha, local))];
    }

    /// <summary>
    /// Un profesional opera solo en un local activo que figure entre los locales
    /// de atención de su ficha activa. La ficha debe pertenecer a la cuenta: una
    /// ficha ajena no autoriza nada. Sin ficha activa no opera en ningún local,
    /// aunque la cuenta siga vigente.
    /// </summary>
    private static bool CanOperateAsProfessional(StaffAccount account, Profesional? ficha, Local local) =>
        ficha is { IsActive: true }
        && ficha.StaffAccountId == account.Id
        && local.IsActive
        && ficha.LocalIds.Contains(local.Id);
}
