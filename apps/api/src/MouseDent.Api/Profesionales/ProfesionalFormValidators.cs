using FluentValidation;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Api.Profesionales;

/// <summary>
/// Reglas de forma del alta y de la edición de una ficha. Los límites y la
/// normalización del número de colegiatura son los de <see cref="DatosDeColegiatura"/>.
/// Las reglas que dependen del estado persistido —la actividad de una especialidad,
/// de un local o de la propia ficha— las aplica el caso de uso.
/// </summary>
public sealed class CreateProfesionalFormValidator : AbstractValidator<CreateProfesionalForm>
{
    public CreateProfesionalFormValidator()
    {
        RuleFor(form => form.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("Indica un correo válido.")
            .MaximumLength(256).WithMessage("El correo no puede superar los 256 caracteres.");

        RuleFor(form => form.DisplayName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar los 120 caracteres.");

        RuleFor(form => form.Colegiatura)
            .Must(DatosDeColegiatura.EsValida)
            .WithMessage($"El número de colegiatura no puede superar los {DatosDeColegiatura.LongitudMaxima} caracteres.");

        RuleFor(form => form.EspecialidadIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Asigna al menos una especialidad.")
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("La especialidad asignada no es válida.");

        RuleFor(form => form.LocalIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Asigna al menos un local de atención.")
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("El local asignado no es válido.");
    }
}

/// <summary>
/// Reglas de forma de la edición de una ficha. Solo cambia la colegiatura y las
/// asignaciones: el nombre, el correo, el teléfono y el perfil pertenecen a la cuenta.
/// </summary>
public sealed class UpdateProfesionalFormValidator : AbstractValidator<UpdateProfesionalForm>
{
    public UpdateProfesionalFormValidator()
    {
        RuleFor(form => form.Colegiatura)
            .Must(DatosDeColegiatura.EsValida)
            .WithMessage($"El número de colegiatura no puede superar los {DatosDeColegiatura.LongitudMaxima} caracteres.");

        RuleFor(form => form.EspecialidadIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Asigna al menos una especialidad.")
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("La especialidad asignada no es válida.");

        RuleFor(form => form.LocalIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Asigna al menos un local de atención.")
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("El local asignado no es válido.");
    }
}
