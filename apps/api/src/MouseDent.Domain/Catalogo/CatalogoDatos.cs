namespace MouseDent.Domain.Catalogo;

/// <summary>
/// Límites y normalización de los datos que comparten especialidades y
/// servicios. Es la única definición de las reglas: la API valida con estos
/// predicados y las entidades los aplican al guardar.
/// </summary>
public static class CatalogoDatos
{
    public const int MinNameLength = 1;

    public const int MaxNameLength = 100;

    public const int MaxDescriptionLength = 500;

    public static bool IsValidName(string? name) =>
        name is not null && name.Trim().Length is >= MinNameLength and <= MaxNameLength;

    public static bool IsValidDescription(string? description) =>
        description is null || description.Trim().Length <= MaxDescriptionLength;

    public static (string Name, string NormalizedName, string? Description) Validate(
        string name,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmedName = name.Trim();
        var trimmedDescription = description?.Trim();
        if (!IsValidName(trimmedName))
        {
            throw new ArgumentException(
                $"El nombre debe tener entre {MinNameLength} y {MaxNameLength} caracteres.",
                nameof(name));
        }

        if (!IsValidDescription(trimmedDescription))
        {
            throw new ArgumentException(
                $"La descripción no puede superar {MaxDescriptionLength} caracteres.",
                nameof(description));
        }

        return (trimmedName, trimmedName.ToUpperInvariant(), trimmedDescription);
    }
}
