using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Detecta el choque con la unicidad de la cuenta: los índices únicos del correo y
/// del nombre de usuario de Identity. SQLite identifica la restricción por su tabla
/// y su columna, no por el nombre del índice. Cualquier otro error de restricción
/// (nulo, verificación o clave foránea) es un defecto del modelo o de la escritura:
/// no se traduce y se relanza, para que no se presente como un correo ocupado.
/// </summary>
internal static class SqliteAccountUniqueness
{
    private const string CorreoNormalizado = "AspNetUsers.NormalizedEmail";
    private const string NombreDeUsuarioNormalizado = "AspNetUsers.NormalizedUserName";

    /// <summary>La escritura chocó con uno de los índices únicos que protegen la cuenta.</summary>
    public static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && (sqlite.Message.Contains(CorreoNormalizado, StringComparison.Ordinal)
            || sqlite.Message.Contains(NombreDeUsuarioNormalizado, StringComparison.Ordinal));
}
