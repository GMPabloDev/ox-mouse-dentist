using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

    private static AuthenticatedUser ToResponse(StaffAccount account) =>
        new(account.Id, account.Email, account.DisplayName, account.Roles);
}
