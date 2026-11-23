namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Patrón de búsqueda parcial para <c>EF.Functions.Like</c>. Neutraliza los
/// comodines del término para que una búsqueda por nombre o correo solo encuentre
/// ese texto y no un patrón elegido por quien consulta.
/// </summary>
internal static class TextSearch
{
    public static string Contains(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
