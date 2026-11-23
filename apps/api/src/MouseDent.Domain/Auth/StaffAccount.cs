namespace MouseDent.Domain.Auth;

/// <summary>
/// Cuenta del personal. Tiene un solo perfil y un correo único, y distingue
/// pendiente (no estableció contraseña), activa e inactiva. No conoce Identity
/// ni la persistencia.
/// </summary>
public sealed class StaffAccount
{
    private readonly List<Guid> _assignedLocalIds = [];

    private StaffAccount()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Teléfono opcional de la cuenta.</summary>
    public string? Phone { get; private set; }

    /// <summary>Perfil único de la cuenta: uno de <see cref="StaffRole.All"/>.</summary>
    public string Profile { get; private set; } = StaffRole.Professional;

    /// <summary>Indica si la cuenta está habilitada. Desactivarla no borra sus datos.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Momento en que la persona estableció su contraseña. Mientras sea nulo la
    /// cuenta queda pendiente: no inicia sesión y no cuenta como administración
    /// disponible. Se conserva al desactivar y se borra al cambiar el correo,
    /// que obliga a establecer una contraseña nueva.
    /// </summary>
    public DateTime? PasswordEstablishedAt { get; private set; }

    /// <summary>
    /// Locales asignados, sin repetidos y ordenados por identificador. Solo las
    /// cuentas de recepción reciben asignaciones; administración accede a todos
    /// los locales por su perfil.
    /// </summary>
    public IReadOnlyList<Guid> AssignedLocalIds => _assignedLocalIds;

    /// <summary>La cuenta todavía no estableció su contraseña.</summary>
    public bool IsPending => PasswordEstablishedAt is null;

    /// <summary>
    /// Estado visible de la cuenta. La desactivación prevalece: una cuenta inactiva
    /// no figura como pendiente aunque no tenga contraseña.
    /// </summary>
    public EstadoDeCuenta Estado =>
        !IsActive
            ? EstadoDeCuenta.Inactiva
            : IsPending
                ? EstadoDeCuenta.Pendiente
                : EstadoDeCuenta.Activa;

    /// <summary>
    /// Versión de la sesión. Cambia cuando una operación debe invalidar los
    /// tokens ya emitidos: desactivar o reactivar la cuenta, cambiar el correo,
    /// cambiar el perfil o establecer una contraseña nueva. Los cambios de
    /// nombre, teléfono y asignaciones no la cambian.
    /// </summary>
    public int SessionVersion { get; private set; } = 1;

    /// <summary>Crea una cuenta pendiente, a la espera de la invitación.</summary>
    public static StaffAccount Invite(
        string displayName,
        string email,
        string? phone,
        string profile,
        IEnumerable<Guid> assignedLocalIds)
    {
        // El perfil se fija antes de editar los datos para que la cuenta nueva
        // conserve la versión inicial de sesión.
        var account = new StaffAccount { Id = Guid.NewGuid(), IsActive = true, Profile = profile };
        account.SetEmail(email);
        account.UpdateDetails(displayName, phone, profile, assignedLocalIds);

        return account;
    }

    /// <summary>Cambia el nombre, el teléfono, el perfil y las asignaciones.</summary>
    public void UpdateDetails(
        string displayName,
        string? phone,
        string profile,
        IEnumerable<Guid> assignedLocalIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assignedLocalIds);

        if (!StaffRole.IsKnown(profile))
        {
            throw new ArgumentException("El perfil debe ser Administrador, Recepcion o Profesional.", nameof(profile));
        }

        var localIds = assignedLocalIds.Distinct().OrderBy(id => id).ToArray();
        if (profile != StaffRole.Reception && localIds.Length > 0)
        {
            throw new ArgumentException(
                "Solo las cuentas de recepción se asignan a locales.",
                nameof(assignedLocalIds));
        }

        var profileChanged = !string.Equals(Profile, profile, StringComparison.Ordinal);

        DisplayName = displayName.Trim();
        Profile = profile;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        if (profileChanged)
        {
            RevokeIssuedSessions();
        }

        _assignedLocalIds.Clear();
        _assignedLocalIds.AddRange(localIds);
    }

    /// <summary>
    /// Cambia el correo y deja la cuenta pendiente. La persona debe establecer
    /// una contraseña desde el enlace enviado al correo nuevo.
    /// </summary>
    public void ChangeEmail(string email)
    {
        SetEmail(email);
        PasswordEstablishedAt = null;
        RevokeIssuedSessions();
    }

    /// <summary>Registra que la persona estableció su contraseña.</summary>
    public void RegisterPasswordEstablished(DateTime moment)
    {
        PasswordEstablishedAt = moment;
        RevokeIssuedSessions();
    }

    /// <summary>
    /// Reconstruye la cuenta desde el almacén. No aplica las reglas de edición:
    /// la persistencia ya contiene cuentas válidas y el almacén solo traduce sus
    /// propios datos.
    /// </summary>
    public static StaffAccount Restore(
        Guid id,
        string email,
        string displayName,
        string? phone,
        string profile,
        bool isActive,
        DateTime? passwordEstablishedAt,
        int sessionVersion,
        IEnumerable<Guid> assignedLocalIds)
    {
        var account = new StaffAccount
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            Phone = phone,
            Profile = profile,
            IsActive = isActive,
            PasswordEstablishedAt = passwordEstablishedAt,
            SessionVersion = sessionVersion,
        };

        account._assignedLocalIds.AddRange(assignedLocalIds.Distinct().OrderBy(localId => localId));

        return account;
    }

    public void Activate()
    {
        IsActive = true;
        RevokeIssuedSessions();
    }

    public void Deactivate()
    {
        IsActive = false;
        RevokeIssuedSessions();
    }

    private void RevokeIssuedSessions() => SessionVersion++;

    private void SetEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
    }
}
