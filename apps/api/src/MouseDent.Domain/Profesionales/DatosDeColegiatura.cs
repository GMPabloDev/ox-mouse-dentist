namespace MouseDent.Domain.Profesionales;

/// <summary>
/// Reglas del número de colegiatura. Es la única definición de los límites y de
/// la normalización: la API valida con estos predicados y la ficha los aplica al
/// guardar. La comparación ignora mayúsculas y espacios exteriores e intermedios,
/// de modo que <c>COP 12345</c> y <c>cop12345</c> son el mismo número; la clave
/// normalizada se persiste con índice único para que la unicidad sobreviva a
/// escrituras simultáneas.
/// </summary>
public static class DatosDeColegiatura
{
    public const int LongitudMaxima = 30;

    /// <summary>
    /// Indica si el valor cabe en la ficha. Un valor nulo, vacío o de solo
    /// espacios significa que la persona no tiene número registrado.
    /// </summary>
    public static bool EsValida(string? numero) =>
        numero is null || numero.Trim().Length <= LongitudMaxima;

    /// <summary>
    /// Clave de comparación: sin espacios exteriores ni intermedios y en
    /// mayúsculas. Dos números equivalentes producen la misma clave.
    /// </summary>
    public static string Normalizar(string numero)
    {
        ArgumentNullException.ThrowIfNull(numero);

        return string.Concat(numero.Where(caracter => !char.IsWhiteSpace(caracter))).ToUpperInvariant();
    }

    /// <summary>
    /// Deja el número listo para guardar: el valor visible sin espacios exteriores
    /// y su clave de comparación. Devuelve <c>null</c> cuando no hay número.
    /// </summary>
    public static NumeroDeColegiatura? Preparar(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return null;
        }

        var visible = numero.Trim();
        if (!EsValida(visible))
        {
            throw new ArgumentException(
                $"El número de colegiatura debe tener entre 1 y {LongitudMaxima} caracteres.",
                nameof(numero));
        }

        return new NumeroDeColegiatura(visible, Normalizar(visible));
    }
}

/// <summary>Número de colegiatura listo para guardar y la clave con la que se compara.</summary>
public sealed record NumeroDeColegiatura(string Visible, string Normalizada);
