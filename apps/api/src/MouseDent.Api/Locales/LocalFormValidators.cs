using FluentValidation;
using MouseDent.Application.Locales;

namespace MouseDent.Api.Locales;

/// <summary>
/// Reglas de los datos que comparten el alta y la edición. El horario se valida
/// con el dominio: <see cref="LocalScheduleReader"/> lee el formulario y
/// <see cref="MouseDent.Domain.Locales.WeeklySchedule.Parse"/> decide.
/// </summary>
public abstract class LocalFormValidator<T> : AbstractValidator<T> where T : LocalDataForm
{
    protected LocalFormValidator()
    {
        RuleFor(form => form.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar los 120 caracteres.");

        RuleFor(form => form.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(200).WithMessage("La dirección no puede superar los 200 caracteres.");

        RuleFor(form => form.Phone)
            .NotEmpty().WithMessage("El teléfono es obligatorio.")
            .MaximumLength(40).WithMessage("El teléfono no puede superar los 40 caracteres.");

        RuleFor(form => form.Schedule)
            .Custom((schedule, context) =>
            {
                foreach (var (field, messages) in LocalScheduleReader.Read(schedule).Errors)
                {
                    foreach (var message in messages)
                    {
                        context.AddFailure(field, message);
                    }
                }
            });

        RuleFor(form => form.Photo)
            .CustomAsync(async (photo, context, cancellationToken) =>
            {
                if (photo is null)
                {
                    return;
                }

                var error = await LocalPhotoRules.CheckAsync(photo, cancellationToken);
                if (error is not null)
                {
                    context.AddFailure(nameof(LocalDataForm.Photo), error);
                }
            });
    }
}

public sealed class CreateLocalFormValidator : LocalFormValidator<CreateLocalForm>;

public sealed class UpdateLocalFormValidator : LocalFormValidator<UpdateLocalForm>
{
    public UpdateLocalFormValidator()
    {
        RuleFor(form => form.PhotoAction)
            .Must(LocalPhotoActionParser.IsKnown)
            .WithMessage("Indica si conservas, sustituyes o quitas la foto con keep, replace o remove.");

        RuleFor(form => form.Photo)
            .NotNull()
            .WithMessage("Envía la foto que sustituye a la actual.")
            .When(form => LocalPhotoActionParser.IsReplace(form.PhotoAction));

        RuleFor(form => form.Photo)
            .Null()
            .WithMessage("La foto solo se envía cuando la sustituyes.")
            .When(form => !LocalPhotoActionParser.IsReplace(form.PhotoAction));
    }
}

/// <summary>Lee la indicación explícita del formulario de edición sobre la foto.</summary>
public static class LocalPhotoActionParser
{
    public static bool TryParse(string? value, out LocalPhotoChange change)
    {
        change = default;

        return !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value.Trim(), ignoreCase: true, out change)
            && Enum.IsDefined(change);
    }

    public static bool IsKnown(string? value) => TryParse(value, out _);

    public static bool IsReplace(string? value) => TryParse(value, out var change) && change == LocalPhotoChange.Replace;
}
