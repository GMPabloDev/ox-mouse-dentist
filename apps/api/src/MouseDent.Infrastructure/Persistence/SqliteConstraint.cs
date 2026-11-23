using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MouseDent.Infrastructure.Persistence;

/// <summary>
/// Detecta el choque con una restricción de SQLite. Solo el correo y el nombre de
/// usuario de una cuenta, el nombre normalizado de un local y la colegiatura
/// normalizada de una ficha tienen índices únicos en este modelo.
/// </summary>
internal static class SqliteConstraint
{
    public static bool IsViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
