using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MouseDent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Profesional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profesionales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StaffAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Colegiatura = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ColegiaturaNormalizada = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesionales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Profesionales_AspNetUsers_StaffAccountId",
                        column: x => x.StaffAccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfesionalEspecialidades",
                columns: table => new
                {
                    ProfesionalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EspecialidadId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfesionalEspecialidades", x => new { x.ProfesionalId, x.EspecialidadId });
                    table.ForeignKey(
                        name: "FK_ProfesionalEspecialidades_Especialidades_EspecialidadId",
                        column: x => x.EspecialidadId,
                        principalTable: "Especialidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfesionalEspecialidades_Profesionales_ProfesionalId",
                        column: x => x.ProfesionalId,
                        principalTable: "Profesionales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfesionalLocales",
                columns: table => new
                {
                    ProfesionalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfesionalLocales", x => new { x.ProfesionalId, x.LocalId });
                    table.ForeignKey(
                        name: "FK_ProfesionalLocales_Locales_LocalId",
                        column: x => x.LocalId,
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfesionalLocales_Profesionales_ProfesionalId",
                        column: x => x.ProfesionalId,
                        principalTable: "Profesionales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profesionales_ColegiaturaNormalizada",
                table: "Profesionales",
                column: "ColegiaturaNormalizada",
                unique: true,
                filter: "\"ColegiaturaNormalizada\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Profesionales_StaffAccountId",
                table: "Profesionales",
                column: "StaffAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfesionalEspecialidades_EspecialidadId",
                table: "ProfesionalEspecialidades",
                column: "EspecialidadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfesionalLocales_LocalId",
                table: "ProfesionalLocales",
                column: "LocalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfesionalEspecialidades");

            migrationBuilder.DropTable(
                name: "ProfesionalLocales");

            migrationBuilder.DropTable(
                name: "Profesionales");
        }
    }
}
