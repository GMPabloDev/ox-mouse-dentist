namespace MouseDent.Domain.Locales;

/// <summary>
/// Reglas del nombre de un local. La unicidad ignora mayúsculas, minúsculas y
/// espacios en los extremos; la clave normalizada se persiste con índice único
/// para que la garantía sobreviva a escrituras concurrentes.
/// </summary>
public static class LocalNames
{
    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
