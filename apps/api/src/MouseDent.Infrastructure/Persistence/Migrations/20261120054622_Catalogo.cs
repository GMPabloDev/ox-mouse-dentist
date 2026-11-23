using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MouseDent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Catalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Especialidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Especialidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Servicios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EspecialidadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servicios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Servicios_Especialidades_EspecialidadId",
                        column: x => x.EspecialidadId,
                        principalTable: "Especialidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Especialidades_NormalizedName",
                table: "Especialidades",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servicios_EspecialidadId_NormalizedName",
                table: "Servicios",
                columns: new[] { "EspecialidadId", "NormalizedName" },
                unique: true);
            // SQLite serializa las escrituras. Los triggers comprueban el estado en la
            // misma escritura, incluso si otra solicitud cambió el registro después de leerlo.
            // El modelo de EF no conoce estos triggers: viven solo aquí y su presencia se
            // comprueba en CatalogoPersistenceTests.
            migrationBuilder.Sql("""
                CREATE TRIGGER Catalogo_Especialidad_Desactivar
                BEFORE UPDATE OF IsActive ON Especialidades
                WHEN NEW.IsActive = 0 AND EXISTS (
                    SELECT 1 FROM Servicios WHERE EspecialidadId = NEW.Id AND IsActive = 1)
                BEGIN SELECT RAISE(ABORT, 'catalogo_especialidad_con_servicios'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Catalogo_Servicio_Crear
                BEFORE INSERT ON Servicios
                WHEN NOT EXISTS (SELECT 1 FROM Especialidades WHERE Id = NEW.EspecialidadId AND IsActive = 1)
                BEGIN SELECT RAISE(ABORT, 'catalogo_destino_inactivo'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Catalogo_Servicio_Actualizar
                BEFORE UPDATE OF EspecialidadId, IsActive ON Servicios
                WHEN (NEW.EspecialidadId <> OLD.EspecialidadId OR NEW.IsActive = 1)
                    AND NOT EXISTS (SELECT 1 FROM Especialidades WHERE Id = NEW.EspecialidadId AND IsActive = 1)
                BEGIN SELECT RAISE(ABORT, 'catalogo_destino_inactivo'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Servicios");

            migrationBuilder.DropTable(
                name: "Especialidades");
        }
    }
}
