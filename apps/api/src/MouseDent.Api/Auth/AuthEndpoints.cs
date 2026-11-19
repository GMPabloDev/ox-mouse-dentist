using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MouseDent.Api.Common;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;

namespace MouseDent.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithName("Login")
            .WithSummary("Inicia sesión y devuelve un token JWT.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Devuelve la cuenta autenticada.")
            .Produces<AuthenticatedUser>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password-recovery", RequestPasswordRecoveryAsync)
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<PasswordRecoveryRequest>>()
            .RequireRateLimiting(RateLimitPolicies.PasswordRecovery)
            .WithName("RequestPasswordRecovery")
            .WithSummary("Solicita un enlace de recuperación sin confirmar si la cuenta existe.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password", ResetPasswordAsync)
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
            .WithName("ResetPassword")
            .WithSummary("Establece la contraseña con un enlace de invitación o recuperación.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        [FromBody] LoginRequest request,
        AuthService auth,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MouseDent.Api.Auth.Login");

        var result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            // No distinguimos entre cuenta inexistente y contraseña incorrecta.
            logger.LogInformation("Intento de acceso fallido para {Email}", request.Email);
            return TypedResults.Problem(
                title: "Credenciales inválidas",
                detail: "El correo o la contraseña no son correctos.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        logger.LogInformation("Acceso correcto de {UserId}", result.Account.Id);

        return TypedResults.Ok(new LoginResponse(
            result.AccessToken,
            result.ExpiresAt,
            ToResponse(result.Account)));
    }

    private static async Task<Results<Ok<AuthenticatedUser>, UnauthorizedHttpResult>> MeAsync(
        ClaimsPrincipal principal,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(AuthClaims.Subject);
        if (!Guid.TryParse(subject, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var account = await auth.FindAsync(userId, cancellationToken);
        return account is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(ToResponse(account));
    }

    /// <summary>
    /// La respuesta es la misma exista la cuenta, esté inactiva o se haya enviado
    /// el enlace. El registro guarda el fallo de correo sin el enlace ni la cuenta.
    /// </summary>
    private static async Task<StatusCodeHttpResult> RequestPasswordRecoveryAsync(
        [FromBody] PasswordRecoveryRequest request,
        StaffAccessService access,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var status = await access.RequestRecoveryAsync(request.Email, cancellationToken);

        if (status is PasswordRecoveryStatus.Failed)
        {
            loggerFactory.CreateLogger("MouseDent.Api.Auth.PasswordRecovery")
                .LogWarning("No se pudo enviar un correo de recuperación de acceso.");
        }

        return TypedResults.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        StaffAccessService access,
        IOptions<IdentityOptions> identity,
        CancellationToken cancellationToken)
    {
        var result = await access.ResetPasswordAsync(request.Token, request.Password, cancellationToken);

        return result.Status switch
        {
            PasswordResetStatus.Reset => TypedResults.NoContent(),
            PasswordResetStatus.LinkUnusable => LinkUnavailableProblem(),
            _ => TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(ResetPasswordRequest.Password)] =
                    [.. StaffPasswordMessages.ToMessages(result.PasswordErrors, identity.Value)],
            }),
        };
    }

    private static ProblemHttpResult LinkUnavailableProblem() => TypedResults.Problem(
        title: "Enlace no disponible",
        detail: "Este enlace ya no está disponible. Solicita recuperar tu acceso o pide a administración una nueva invitación.",
        statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// La cuenta tiene un solo perfil; el contrato conserva la lista de roles del
    /// token con un único elemento para no romper al cliente actual.
    /// </summary>
    private static AuthenticatedUser ToResponse(StaffAccount account) =>
        new(account.Id, account.Email, account.DisplayName, [account.Profile]);
}
