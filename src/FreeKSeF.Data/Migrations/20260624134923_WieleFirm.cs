using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeKSeF.Data.Migrations
{
    /// <inheritdoc />
    public partial class WieleFirm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_Kierunek_DataWystawienia",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_Nip",
                table: "Contractors");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Contractors",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: istniejace (jednofirmowe) dane przypisujemy do pierwszej firmy,
            // aby po aktualizacji nadal byly widoczne.
            migrationBuilder.Sql(
                "UPDATE Invoices SET CompanyId = (SELECT MIN(Id) FROM Companies) " +
                "WHERE CompanyId = 0 AND EXISTS (SELECT 1 FROM Companies);");
            migrationBuilder.Sql(
                "UPDATE Contractors SET CompanyId = (SELECT MIN(Id) FROM Companies) " +
                "WHERE CompanyId = 0 AND EXISTS (SELECT 1 FROM Companies);");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_Kierunek_DataWystawienia",
                table: "Invoices",
                columns: new[] { "CompanyId", "Kierunek", "DataWystawienia" });

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_CompanyId_Nip",
                table: "Contractors",
                columns: new[] { "CompanyId", "Nip" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_Kierunek_DataWystawienia",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_CompanyId_Nip",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Contractors");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Kierunek_DataWystawienia",
                table: "Invoices",
                columns: new[] { "Kierunek", "DataWystawienia" });

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_Nip",
                table: "Contractors",
                column: "Nip");
        }
    }
}
