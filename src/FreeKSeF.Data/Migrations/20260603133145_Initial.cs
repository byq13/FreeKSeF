using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeKSeF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nip = table.Column<string>(type: "TEXT", nullable: false),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false),
                    KodKraju = table.Column<string>(type: "TEXT", nullable: false),
                    AdresL1 = table.Column<string>(type: "TEXT", nullable: false),
                    AdresL2 = table.Column<string>(type: "TEXT", nullable: true),
                    NrRachunku = table.Column<string>(type: "TEXT", nullable: true),
                    Srodowisko = table.Column<int>(type: "INTEGER", nullable: false),
                    KsefTokenProtected = table.Column<string>(type: "TEXT", nullable: true),
                    UtworzonoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nip = table.Column<string>(type: "TEXT", nullable: true),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false),
                    KodKraju = table.Column<string>(type: "TEXT", nullable: false),
                    AdresL1 = table.Column<string>(type: "TEXT", nullable: false),
                    AdresL2 = table.Column<string>(type: "TEXT", nullable: true),
                    UtworzonoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kierunek = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Numer = table.Column<string>(type: "TEXT", nullable: false),
                    NumerKsef = table.Column<string>(type: "TEXT", nullable: true),
                    NumerReferencyjny = table.Column<string>(type: "TEXT", nullable: true),
                    DataWystawienia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataSprzedazy = table.Column<DateTime>(type: "TEXT", nullable: true),
                    KontrahentNip = table.Column<string>(type: "TEXT", nullable: true),
                    KontrahentNazwa = table.Column<string>(type: "TEXT", nullable: false),
                    Waluta = table.Column<string>(type: "TEXT", nullable: false),
                    SumaNetto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SumaVat = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SumaBrutto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Xml = table.Column<string>(type: "TEXT", nullable: false),
                    UpoXml = table.Column<string>(type: "TEXT", nullable: true),
                    UtworzonoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WyslanoUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KsefLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CzasUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Operacja = table.Column<string>(type: "TEXT", nullable: false),
                    Poziom = table.Column<string>(type: "TEXT", nullable: false),
                    Komunikat = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KsefLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Lp = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false),
                    Jednostka = table.Column<string>(type: "TEXT", nullable: false),
                    Ilosc = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CenaNetto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Stawka = table.Column<int>(type: "INTEGER", nullable: false),
                    WartoscNetto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    KwotaVat = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_Nip",
                table: "Contractors",
                column: "Nip");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Kierunek_DataWystawienia",
                table: "Invoices",
                columns: new[] { "Kierunek", "DataWystawienia" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_NumerKsef",
                table: "Invoices",
                column: "NumerKsef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Contractors");

            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "KsefLogs");

            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
