using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeKSeF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Produkty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false),
                    Jednostka = table.Column<string>(type: "TEXT", nullable: false),
                    CenaNetto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Stawka = table.Column<int>(type: "INTEGER", nullable: false),
                    Pkwiu = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_Nazwa",
                table: "Products",
                columns: new[] { "CompanyId", "Nazwa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
