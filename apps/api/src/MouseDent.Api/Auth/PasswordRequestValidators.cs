using FluentValidation;

namespace MouseDent.Api.Auth;

/// <summary>
/// Reglas de forma de las solicitudes públicas de contraseña. La política de la
/// contraseña nueva no se duplica aquí: la aplica Identity al consumir el enlace.
/// </summary>
public sealed class PasswordRecoveryRequestValidator : AbstractValidator<PasswordRecoveryRequest>
{
    public PasswordRecoveryRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("Indica un correo válido.")
            .MaximumLength(256).WithMessage("El correo no puede superar los 256 caracteres.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("El enlace no es válido.")
            .MaximumLength(200).WithMessage("El enlace no es válido.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar los 128 caracteres.");
    }
}
