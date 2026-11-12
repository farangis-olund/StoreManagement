using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261110000000_AddOfficialMonthlyPlans")]
public sealed class AddOfficialMonthlyPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("OfficialMonthlyPlans", columns: table => new
        {
            CustomerId = table.Column<string>("nvarchar(450)", maxLength: 450, nullable: false),
            Month = table.Column<DateTime>("date", nullable: false),
            Amount = table.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_OfficialMonthlyPlans", x => new { x.CustomerId, x.Month });
            table.ForeignKey("FK_OfficialMonthlyPlans_Customers_CustomerId", x => x.CustomerId,
                "Customers", "Id", onDelete: ReferentialAction.Restrict);
            table.CheckConstraint("CK_OfficialMonthlyPlans_Amount", "[Amount] > 0");
            table.CheckConstraint("CK_OfficialMonthlyPlans_Month", "DAY([Month]) = 1");
        });
        migrationBuilder.CreateIndex("IX_OfficialMonthlyPlans_Month", "OfficialMonthlyPlans", "Month");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OfficialMonthlyPlans");
}
