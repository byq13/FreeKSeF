using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeKSeF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kursy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kursy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kod = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kurs = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Tabela = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kursy", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kursy_Kod_Data",
                table: "Kursy",
                columns: new[] { "Kod", "Data" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kursy");
        }
    }
}
