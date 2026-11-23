using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Caso de uso de los enlaces de invitación y recuperación. Emite un token
/// aleatorio, guarda solo su hash, invalida los enlaces anteriores de la cuenta
/// y envía el correo con la URL pública. Solicitar recuperación no cambia las
/// sesiones: solo completar la contraseña las invalida.
/// </summary>
public sealed class StaffAccessService(
    IStaffAccounts accounts,
    IStaffAccessLinks links,
    IEmailSender email,
    StaffAccessOptions options,
    TimeProvider clock)
{
    /// <summary>
    /// Envía una invitación a la cuenta. Devuelve <c>false</c> cuando el correo no
    /// se pudo entregar; el enlace anterior ya no sirve en ese caso. El resultado
    /// queda guardado en la cuenta como marca del último envío fallido, que dura
    /// hasta que un envío se entregue, la cuenta establezca su contraseña o cambie
    /// su correo. Registrar o limpiar la marca no cambia la versión de sesión.
    /// </summary>
    public async Task<bool> SendInvitationAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        var delivered = await IssueAndSendAsync(account, AccessLinkPurpose.Invitation, cancellationToken);

        if (delivered)
        {
            await accounts.ClearInvitationFailureAsync(account.Id, cancellationToken);
        }
        else
        {
            await accounts.RecordInvitationFailureAsync(
                account.Id,
                clock.GetUtcNow().UtcDateTime,
                cancellationToken);
        }

        return delivered;
    }

    /// <summary>
    /// Atiende una solicitud pública de recuperación. No revela si la cuenta
    /// existe y no envía enlaces utilizables a cuentas inactivas.
    /// </summary>
    public async Task<PasswordRecoveryStatus> RequestRecoveryAsync(
        string address,
        CancellationToken cancellationToken)
    {
        if (!HasValidPublicBaseUrl())
        {
            return PasswordRecoveryStatus.Failed;
        }

        var account = await accounts.FindByEmailAsync(address.Trim(), cancellationToken);

        if (account is null || !account.IsActive)
        {
            return PasswordRecoveryStatus.Ignored;
        }

        return await IssueAndSendAsync(account, AccessLinkPurpose.Recovery, cancellationToken)
            ? PasswordRecoveryStatus.Sent
            : PasswordRecoveryStatus.Failed;
    }

    /// <summary>Consume un enlace de invitación o recuperación.</summary>
    public Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string password,
        CancellationToken cancellationToken) =>
        links.ResetPasswordAsync(HashToken(token.Trim()), password, cancellationToken);

    private async Task<bool> IssueAndSendAsync(
        StaffAccount account,
        AccessLinkPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (!HasValidPublicBaseUrl())
        {
            return false;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        // El mensaje se compone antes de tocar la base: un error de configuración
        // no invalida los enlaces pendientes de la cuenta.
        var message = Compose(account, token, purpose);
        var link = StaffAccessLink.Issue(account.Id, HashToken(token), clock.GetUtcNow().UtcDateTime);

        // El enlace se emite antes de enviar: si el correo falla, los enlaces
        // anteriores tampoco sirven y administración puede reenviar.
        if (!await links.ReplacePendingAsync(link, account, purpose is AccessLinkPurpose.Invitation, cancellationToken))
        {
            return false;
        }

        try
        {
            await email.SendAsync(message, cancellationToken);
            return true;
        }
        catch (EmailDeliveryException)
        {
            return false;
        }
    }

    private EmailMessage Compose(StaffAccount account, string token, AccessLinkPurpose purpose)
    {
        var link = AccessLink(token);

        return purpose is AccessLinkPurpose.Invitation
            ? new EmailMessage(
                account.Email,
                "Tu invitación a MouseDent",
                $"""
                Hola, {account.DisplayName}:

                Administración te invita a MouseDent. Crea tu contraseña con este enlace:
                {link}

                El enlace es de un solo uso y vence en 24 horas. Si no esperabas este correo, ignóralo.
                """)
            : new EmailMessage(
                account.Email,
                "Recupera tu acceso a MouseDent",
                $"""
                Hola, {account.DisplayName}:

                Recibimos una solicitud para recuperar tu acceso a MouseDent. Crea una contraseña nueva con este enlace:
                {link}

                El enlace es de un solo uso y vence en 24 horas. Si no solicitaste el cambio, ignora este correo: tu contraseña actual sigue funcionando.
                """);
    }

    private bool HasValidPublicBaseUrl() =>
        !string.IsNullOrWhiteSpace(options.PublicBaseUrl)
        && options.PublicBaseUrl == options.PublicBaseUrl.Trim()
        && Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);

    private string AccessLink(string token)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');

        if (baseUrl.Length == 0)
        {
            throw new InvalidOperationException(
                "Falta configurar StaffAccess:PublicBaseUrl para construir los enlaces del correo.");
        }

        return $"{baseUrl}{options.ResetPath}?token={Uri.EscapeDataString(token)}";
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private enum AccessLinkPurpose
    {
        Invitation,
        Recovery,
    }
}

/// <summary>Resultado de atender una solicitud pública de recuperación.</summary>
public enum PasswordRecoveryStatus
{
    /// <summary>La cuenta admitía el enlace y el correo se envió.</summary>
    Sent,

    /// <summary>No hay cuenta utilizable con ese correo; la respuesta es la misma.</summary>
    Ignored,

    /// <summary>La cuenta admitía el enlace, pero el correo no se pudo entregar.</summary>
    Failed,
}
