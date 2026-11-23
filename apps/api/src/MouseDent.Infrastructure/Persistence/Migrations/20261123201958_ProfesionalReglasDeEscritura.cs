using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MouseDent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfesionalReglasDeEscritura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite serializa las escrituras. Los triggers comprueban el estado en la misma
            // escritura, incluso si otra solicitud desactivó la especialidad o el local después
            // de que la ficha los leyera, y repiten el mínimo de una ficha activa. El modelo de
            // EF no conoce estos triggers: viven solo aquí, su presencia se comprueba en
            // ProfesionalesPersistenceTests y EfProfesionales traduce sus mensajes.
            //
            // Los dos triggers de asignación solo rechazan un destino que existe y está inactivo:
            // un identificador inexistente lo rechaza la clave foránea, como antes. La creación de
            // una ficha no se vigila aquí porque EF inserta la ficha antes que sus asignaciones y
            // el trigger no podría verlas; al crear, la comprobación vive en Domain.
            migrationBuilder.Sql("""
                CREATE TRIGGER Profesional_Especialidad_Asignar
                BEFORE INSERT ON ProfesionalEspecialidades
                WHEN EXISTS (
                    SELECT 1 FROM Especialidades WHERE Id = NEW.EspecialidadId AND IsActive = 0)
                BEGIN SELECT RAISE(ABORT, 'profesional_especialidad_inactiva'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Profesional_Local_Asignar
                BEFORE INSERT ON ProfesionalLocales
                WHEN EXISTS (SELECT 1 FROM Locales WHERE Id = NEW.LocalId AND IsActive = 0)
                BEGIN SELECT RAISE(ABORT, 'profesional_local_inactivo'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Profesional_Activar
                BEFORE UPDATE OF IsActive ON Profesionales
                WHEN NEW.IsActive = 1 AND (
                    NOT EXISTS (
                        SELECT 1
                        FROM ProfesionalEspecialidades
                        JOIN Especialidades ON Especialidades.Id = ProfesionalEspecialidades.EspecialidadId
                        WHERE ProfesionalEspecialidades.ProfesionalId = NEW.Id
                            AND Especialidades.IsActive = 1)
                    OR NOT EXISTS (
                        SELECT 1
                        FROM ProfesionalLocales
                        JOIN Locales ON Locales.Id = ProfesionalLocales.LocalId
                        WHERE ProfesionalLocales.ProfesionalId = NEW.Id
                            AND Locales.IsActive = 1))
                BEGIN SELECT RAISE(ABORT, 'profesional_activacion_incompleta'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER Profesional_Activar;");
            migrationBuilder.Sql("DROP TRIGGER Profesional_Local_Asignar;");
            migrationBuilder.Sql("DROP TRIGGER Profesional_Especialidad_Asignar;");
        }
    }
}
