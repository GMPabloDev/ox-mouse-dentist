using Microsoft.EntityFrameworkCore;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Modelo de lectura de la lista de profesionales. Une las fichas con sus cuentas,
/// con las especialidades y con los locales, de modo que el nombre, el correo y el
/// teléfono salen de la cuenta y el estado de cada asignación, del catálogo y del
/// local. El estado de la cuenta se deriva de su habilitación y de la contraseña
/// establecida, y la marca de invitación fallida solo se informa mientras la cuenta
/// siga pendiente.
/// </summary>
public sealed class EfListadoDeProfesionales(MouseDentDbContext database) : IListadoDeProfesionales
{
    public async Task<IReadOnlyList<ProfesionalResumen>> ListAsync(
        ProfesionalFiltro filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var filas =
            from ficha in database.Profesionales.AsNoTracking()
            join cuenta in database.Users.AsNoTracking() on ficha.StaffAccountId equals cuenta.Id
            select new { Ficha = ficha, Cuenta = cuenta };

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var pattern = TextSearch.Contains(filtro.Search.Trim());
            filas = filas.Where(fila =>
                EF.Functions.Like(fila.Cuenta.DisplayName, pattern, "\\")
                || EF.Functions.Like(fila.Cuenta.Email!, pattern, "\\"));
        }

        if (filtro.EspecialidadId is { } especialidadId)
        {
            filas = filas.Where(fila =>
                fila.Ficha.Especialidades.Any(asignada => asignada.EspecialidadId == especialidadId));
        }

        if (filtro.LocalId is { } localId)
        {
            filas = filas.Where(fila =>
                fila.Ficha.Locales.Any(asignado => asignado.LocalId == localId));
        }

        if (filtro.FichaActiva is { } fichaActiva)
        {
            filas = filas.Where(fila => fila.Ficha.IsActive == fichaActiva);
        }

        // El estado visible de la cuenta se define una sola vez: la inactiva prevalece
        // sobre la pendiente y la pendiente sobre la activa. El filtro y el resumen lo
        // usan desde esta proyección, así que no hay dos versiones de la regla.
        var conEstado = filas.Select(fila => new
        {
            fila.Ficha,
            fila.Cuenta,
            Estado = !fila.Cuenta.IsActive
                ? EstadoDeCuenta.Inactiva
                : fila.Cuenta.PasswordEstablishedAt == null
                    ? EstadoDeCuenta.Pendiente
                    : EstadoDeCuenta.Activa,
        });

        if (filtro.AccountStatus is { } estado)
        {
            conEstado = conEstado.Where(fila => fila.Estado == estado);
        }

        return await conEstado
            .OrderBy(fila => fila.Cuenta.DisplayName)
            .ThenBy(fila => fila.Cuenta.Email)
            .Select(fila => new ProfesionalResumen(
                fila.Ficha.Id,
                fila.Cuenta.Id,
                fila.Cuenta.DisplayName,
                fila.Cuenta.Email ?? string.Empty,
                fila.Cuenta.PhoneNumber,
                fila.Ficha.Colegiatura,
                fila.Ficha.IsActive,
                fila.Estado,
                // La marca solo se informa mientras la cuenta siga pendiente.
                fila.Estado == EstadoDeCuenta.Pendiente && fila.Cuenta.InvitationFailedAt != null,
                fila.Ficha.Especialidades
                    .Join(
                        database.Especialidades,
                        asignada => asignada.EspecialidadId,
                        especialidad => especialidad.Id,
                        (asignada, especialidad) => especialidad)
                    .OrderBy(especialidad => especialidad.Name)
                    .Select(especialidad => new ProfesionalResumenEspecialidad(
                        especialidad.Id,
                        especialidad.Name,
                        especialidad.IsActive))
                    .ToList(),
                fila.Ficha.Locales
                    .Join(
                        database.Locales,
                        asignado => asignado.LocalId,
                        local => local.Id,
                        (asignado, local) => local)
                    .OrderBy(local => local.Name)
                    .Select(local => new ProfesionalResumenLocal(local.Id, local.Name, local.IsActive))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
