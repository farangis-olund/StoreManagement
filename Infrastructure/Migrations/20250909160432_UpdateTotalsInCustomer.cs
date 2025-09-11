using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTotalsInCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalPurchaseAmount",
                table: "Customers",
                newName: "DailyRepaymentCoefficient");

            migrationBuilder.RenameColumn(
                name: "PlannedPurchaseAmount",
                table: "Customers",
                newName: "DailyPurchaseCoefficient");

            migrationBuilder.RenameColumn(
                name: "MonthlyRepaymentRate",
                table: "Customers",
                newName: "DailyPlannedPurchaseCoefficient");

            migrationBuilder.RenameColumn(
                name: "ExcludeMonthlyRepayment",
                table: "Customers",
                newName: "ExcludeDailyRepayment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExcludeDailyRepayment",
                table: "Customers",
                newName: "ExcludeMonthlyRepayment");

            migrationBuilder.RenameColumn(
                name: "DailyRepaymentCoefficient",
                table: "Customers",
                newName: "TotalPurchaseAmount");

            migrationBuilder.RenameColumn(
                name: "DailyPurchaseCoefficient",
                table: "Customers",
                newName: "PlannedPurchaseAmount");

            migrationBuilder.RenameColumn(
                name: "DailyPlannedPurchaseCoefficient",
                table: "Customers",
                newName: "MonthlyRepaymentRate");
        }
    }
}
