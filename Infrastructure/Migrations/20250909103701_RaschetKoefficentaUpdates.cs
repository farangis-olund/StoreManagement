using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RaschetKoefficentaUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KoefNeobZakupaDni",
                table: "RaschetKoefficenta",
                newName: "KoefZakupaDni");

            migrationBuilder.RenameColumn(
                name: "KoefNeobZakupa",
                table: "RaschetKoefficenta",
                newName: "KoefZakupa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KoefZakupaDni",
                table: "RaschetKoefficenta",
                newName: "KoefNeobZakupaDni");

            migrationBuilder.RenameColumn(
                name: "KoefZakupa",
                table: "RaschetKoefficenta",
                newName: "KoefNeobZakupa");
        }
    }
}
