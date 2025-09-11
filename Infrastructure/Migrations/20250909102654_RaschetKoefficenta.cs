using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RaschetKoefficenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaschetKoefficenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KoefNeobZakupa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefNeobZakupaDni = table.Column<int>(type: "int", nullable: false),
                    KoefEzhPogashOstatokNach1 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashOstatokKon1 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashDin1 = table.Column<int>(type: "int", nullable: false),
                    KoefEzhPogashOstatokNach2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashOstatokKon2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashDin2 = table.Column<int>(type: "int", nullable: false),
                    KoefEzhPogashOstatokNach3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashOstatokKon3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashDin3 = table.Column<int>(type: "int", nullable: false),
                    KoefEzhPogashOstatokNach4 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashOstatokKon4 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashDin4 = table.Column<int>(type: "int", nullable: false),
                    KoefEzhPogashOstatokNach5 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashOstatokKon5 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefEzhPogashDin5 = table.Column<int>(type: "int", nullable: false),
                    KoefZaplanZakup = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KoefZaplanZakupDni = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaschetKoefficenta", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaschetKoefficenta");
        }
    }
}
