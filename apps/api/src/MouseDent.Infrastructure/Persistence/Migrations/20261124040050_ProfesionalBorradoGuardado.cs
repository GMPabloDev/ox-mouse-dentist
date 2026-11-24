using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MouseDent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfesionalBorradoGuardado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los triggers de inserción y el de activación no cubren el borrado de una
            // asignación. Una solicitud que leyó la ficha inactiva puede retirar sus
            // especialidades o sus locales mientras otra la reactiva, y dejarla activa sin
            // el mínimo. Estos dos triggers cierran esa ventana con la misma garantía
            // estructural que los demás: si la ficha padre está activa y el borrado la deja
            // sin ninguna especialidad activa o sin ningún local activo, la escritura aborta.
            //
            // El modelo de EF no conoce estos triggers: viven solo aquí, su presencia se
            // comprueba en ProfesionalesPersistenceTests y EfProfesionales traduce el mensaje.
            migrationBuilder.Sql("""
                CREATE TRIGGER Profesional_Especialidad_Borrar
                BEFORE DELETE ON ProfesionalEspecialidades
                WHEN EXISTS (
                    SELECT 1 FROM Profesionales WHERE Id = OLD.ProfesionalId AND IsActive = 1)
                    AND NOT EXISTS (
                        SELECT 1
                        FROM ProfesionalEspecialidades
                        JOIN Especialidades ON Especialidades.Id = ProfesionalEspecialidades.EspecialidadId
                        WHERE ProfesionalEspecialidades.ProfesionalId = OLD.ProfesionalId
                            AND ProfesionalEspecialidades.EspecialidadId <> OLD.EspecialidadId
                            AND Especialidades.IsActive = 1)
                BEGIN SELECT RAISE(ABORT, 'profesional_activacion_incompleta'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Profesional_Local_Borrar
                BEFORE DELETE ON ProfesionalLocales
                WHEN EXISTS (
                    SELECT 1 FROM Profesionales WHERE Id = OLD.ProfesionalId AND IsActive = 1)
                    AND NOT EXISTS (
                        SELECT 1
                        FROM ProfesionalLocales
                        JOIN Locales ON Locales.Id = ProfesionalLocales.LocalId
                        WHERE ProfesionalLocales.ProfesionalId = OLD.ProfesionalId
                            AND ProfesionalLocales.LocalId <> OLD.LocalId
                            AND Locales.IsActive = 1)
                BEGIN SELECT RAISE(ABORT, 'profesional_activacion_incompleta'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER Profesional_Local_Borrar;");
            migrationBuilder.Sql("DROP TRIGGER Profesional_Especialidad_Borrar;");
        }
    }
}
