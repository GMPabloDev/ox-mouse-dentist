using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MouseDent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordEstablishedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Conserva las cuentas existentes: el perfil sale del rol que ya tenían,
            // la cuenta sigue activa y las que ya tienen contraseña dejan de estar
            // pendientes. Sin este relleno, la cuenta de administración sembrada en
            // SPEC 01 quedaría pendiente e inactiva.
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "Profile" = COALESCE(
                    (SELECT role."Name"
                     FROM "AspNetUserRoles" AS assignment
                     INNER JOIN "AspNetRoles" AS role ON role."Id" = assignment."RoleId"
                     WHERE assignment."UserId" = "AspNetUsers"."Id"
                       AND role."Name" IN ('Administrador', 'Recepcion', 'Profesional')
                     LIMIT 1),
                    'Profesional')
                WHERE "Profile" = '' OR "Profile" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "PasswordEstablishedAt" = CURRENT_TIMESTAMP
                WHERE "PasswordHash" IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "StaffLocalAssignments",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLocalAssignments", x => new { x.UserId, x.LocalId });
                    table.ForeignKey(
                        name: "FK_StaffLocalAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffLocalAssignments_Locales_LocalId",
                        column: x => x.LocalId,
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffLocalAssignments_LocalId",
                table: "StaffLocalAssignments",
                column: "LocalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffLocalAssignments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasswordEstablishedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Profile",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "AspNetUsers");
        }
    }
}
