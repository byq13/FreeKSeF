using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeKSeF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Ustawienia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ustawienia",
                columns: table => new
                {
                    Klucz = table.Column<string>(type: "TEXT", nullable: false),
                    Wartosc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ustawienia", x => x.Klucz);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ustawienia");
        }
    }
}
