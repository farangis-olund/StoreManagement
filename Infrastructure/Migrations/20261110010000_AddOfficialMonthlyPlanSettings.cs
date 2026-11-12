using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261110010000_AddOfficialMonthlyPlanSettings")]
public sealed class AddOfficialMonthlyPlanSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("OfficialMonthlyPlanSettings", columns: table => new
        {
            Id = table.Column<int>("int", nullable: false),
            NonOfficialThreshold = table.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_OfficialMonthlyPlanSettings", x => x.Id);
            table.CheckConstraint("CK_OfficialMonthlyPlanSettings_Id", "[Id] = 1");
            table.CheckConstraint("CK_OfficialMonthlyPlanSettings_Threshold", "[NonOfficialThreshold] >= 0");
        });
        migrationBuilder.InsertData(table: "OfficialMonthlyPlanSettings", columns: new[] { "Id", "NonOfficialThreshold" },
            columnTypes: new[] { "int", "decimal(18,2)" }, values: new object[] { 1, 0m });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OfficialMonthlyPlanSettings");
}
