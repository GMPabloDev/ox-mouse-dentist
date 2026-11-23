using FluentValidation;
using MouseDent.Domain.Catalogo;

namespace MouseDent.Api.Catalogo;

/// <summary>
/// Reglas de los datos que comparten el alta y la edición. Los límites viven en
/// <see cref="CatalogoDatos"/> para que API y dominio no puedan separarse.
/// </summary>
public sealed class EspecialidadRequestValidator : AbstractValidator<EspecialidadRequest>
{
    public EspecialidadRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(CatalogoDatos.IsValidName)
            .WithMessage(
                $"El nombre debe tener entre {CatalogoDatos.MinNameLength} y {CatalogoDatos.MaxNameLength} caracteres.");

        RuleFor(request => request.Description)
            .Must(CatalogoDatos.IsValidDescription)
            .WithMessage(
                $"La descripción no puede superar {CatalogoDatos.MaxDescriptionLength} caracteres.");
    }
}

public sealed class ServicioRequestValidator : AbstractValidator<ServicioRequest>
{
    public ServicioRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(CatalogoDatos.IsValidName)
            .WithMessage(
                $"El nombre debe tener entre {CatalogoDatos.MinNameLength} y {CatalogoDatos.MaxNameLength} caracteres.");

        RuleFor(request => request.Description)
            .Must(CatalogoDatos.IsValidDescription)
            .WithMessage(
                $"La descripción no puede superar {CatalogoDatos.MaxDescriptionLength} caracteres.");

        RuleFor(request => request.EspecialidadId)
            .NotEmpty()
            .WithMessage("Selecciona una especialidad.");
    }
}
