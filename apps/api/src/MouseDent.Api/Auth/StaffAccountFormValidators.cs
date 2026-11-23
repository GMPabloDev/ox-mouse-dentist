using FluentValidation;
using MouseDent.Domain.Auth;

namespace MouseDent.Api.Auth;

/// <summary>
/// Reglas de forma de los datos de una cuenta. Las reglas que dependen del
/// estado persistido, como la actividad de un local, las aplica el caso de uso.
/// </summary>
public abstract class StaffAccountFormValidator<T> : AbstractValidator<T> where T : StaffAccountForm
{
    /// <param name="profileMessage">
    /// Perfiles que ofrece el formulario. El perfil <c>Profesional</c> sigue siendo un
    /// valor conocido para que una edición pueda conservarlo, pero el alta no lo crea.
    /// </param>
    protected StaffAccountFormValidator(string profileMessage)
    {
        RuleFor(form => form.DisplayName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar los 120 caracteres.");

        RuleFor(form => form.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("Indica un correo válido.")
            .MaximumLength(256).WithMessage("El correo no puede superar los 256 caracteres.");

        RuleFor(form => form.Phone)
            .MaximumLength(40).WithMessage("El teléfono no puede superar los 40 caracteres.");

        RuleFor(form => form.Profile)
            .NotEmpty().WithMessage("El perfil es obligatorio.")
            .Must(StaffRole.IsKnown)
            .WithMessage(profileMessage);

        RuleFor(form => form)
            .Custom((form, context) =>
            {
                var localIds = form.AssignedLocalIds ?? [];

                if (form.Profile != StaffRole.Reception && localIds.Count > 0)
                {
                    context.AddFailure(
                        nameof(StaffAccountForm.AssignedLocalIds),
                        "Solo las cuentas de recepción se asignan a locales.");
                }
            });
    }
}

public sealed class CreateStaffAccountFormValidator : StaffAccountFormValidator<CreateStaffAccountForm>
{
    public CreateStaffAccountFormValidator()
        : base("Elige uno de los perfiles del alta: Administrador o Recepcion.")
    {
        RuleFor(form => form.AssignedLocalIds)
            .Must(localIds => localIds is { Count: > 0 })
            .When(form => form.Profile == StaffRole.Reception)
            .WithMessage("Una cuenta de recepción requiere al menos un local activo.");
    }
}

public sealed class UpdateStaffAccountFormValidator : StaffAccountFormValidator<UpdateStaffAccountForm>
{
    public UpdateStaffAccountFormValidator()
        : base("Elige uno de los perfiles: Administrador, Recepcion o Profesional.")
    {
    }
}
