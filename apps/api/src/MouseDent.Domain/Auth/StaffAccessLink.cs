namespace MouseDent.Domain.Auth;

/// <summary>
/// Enlace de un solo uso para establecer la contraseña, tanto de invitación como
/// de recuperación. Solo se guarda el hash del token: una lectura de la base no
/// permite construir la URL. Emitir un enlace nuevo invalida los anteriores.
/// </summary>
public sealed class StaffAccessLink
{
    /// <summary>Vigencia de un enlace emitido.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private StaffAccessLink()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Cuenta cuyo acceso habilita el enlace.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Hash del token. El token en claro no se persiste.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Momento en que se consumió el enlace. Nulo mientras no se haya usado.</summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>Un enlace sirve una sola vez y dentro de su vigencia.</summary>
    public bool IsUsable(DateTime moment) => UsedAt is null && moment < ExpiresAt;

    public static StaffAccessLink Issue(Guid accountId, string tokenHash, DateTime issuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new StaffAccessLink
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            TokenHash = tokenHash,
            CreatedAt = issuedAt,
            ExpiresAt = issuedAt.Add(Lifetime),
        };
    }
}
