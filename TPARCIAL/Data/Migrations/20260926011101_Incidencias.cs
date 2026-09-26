using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TPARCIAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class Incidencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Estaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaReporte = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstacionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidencias_Estaciones_EstacionId",
                        column: x => x.EstacionId,
                        principalTable: "Estaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Estaciones",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Estación Central" },
                    { 2, "Estación Norte" },
                    { 3, "Estación Sur" }
                });

            migrationBuilder.InsertData(
                table: "Incidencias",
                columns: new[] { "Id", "Descripcion", "EstacionId", "Estado", "FechaReporte" },
                values: new object[,]
                {
                    { 1, "Escalera mecánica detenida", 1, "Abierta", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Torniquete de acceso averiado", 1, "Cerrada", new DateTime(2026, 9, 2, 9, 30, 0, 0, DateTimeKind.Utc) },
                    { 3, "Fuga de agua en andén", 2, "Abierta", new DateTime(2026, 9, 3, 7, 15, 0, 0, DateTimeKind.Utc) },
                    { 4, "Iluminación deficiente en pasillo", 3, "EnProceso", new DateTime(2026, 9, 4, 18, 45, 0, 0, DateTimeKind.Utc) },
                    { 5, "Máquina expendedora sin servicio", 3, "Abierta", new DateTime(2026, 9, 5, 12, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidencias_EstacionId",
                table: "Incidencias",
                column: "EstacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Incidencias");

            migrationBuilder.DropTable(
                name: "Estaciones");
        }
    }
}
