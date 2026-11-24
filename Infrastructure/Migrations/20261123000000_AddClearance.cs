using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261123000000_AddClearance")]
public sealed class AddClearance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ClearanceProducts", columns: table => new
        {
            ArticleNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, unicode: false, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ClearanceProducts", x => x.ArticleNumber);
            table.ForeignKey("FK_ClearanceProducts_Products_ArticleNumber", x => x.ArticleNumber,
                "Products", "ArticleNumber", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateTable("ClearanceBonusReports", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            FromDate = table.Column<DateTime>(type: "date", nullable: false),
            ToDate = table.Column<DateTime>(type: "date", nullable: false),
            Percent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            TotalQuantity = table.Column<long>(type: "bigint", nullable: false),
            TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            TotalBonus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ClearanceBonusReports", x => x.Id);
            table.CheckConstraint("CK_ClearanceBonusReports_Period", "[FromDate] <= [ToDate]");
            table.CheckConstraint("CK_ClearanceBonusReports_Percent", "[Percent] > 0 AND [Percent] <= 100");
        });
        migrationBuilder.CreateIndex("IX_ClearanceBonusReports_FromDate_ToDate", "ClearanceBonusReports",
            new[] { "FromDate", "ToDate" }, unique: true);
        migrationBuilder.CreateTable("ClearanceBonusLines", columns: table => new
        {
            ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            ArticleNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, unicode: false, nullable: false),
            SellerName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
            Quantity = table.Column<long>(type: "bigint", nullable: false),
            Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Bonus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ClearanceBonusLines", x => new { x.ReportId, x.UserId, x.ArticleNumber });
            table.ForeignKey("FK_ClearanceBonusLines_ClearanceBonusReports_ReportId", x => x.ReportId,
                "ClearanceBonusReports", "Id", onDelete: ReferentialAction.Cascade);
        });
        // Grant the new tab once to existing administration roles; future revocations are preserved.
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WITH (UPDLOCK, HOLDLOCK) WHERE [Key] = N'Admin.Clearance')
            BEGIN
                INSERT INTO [dbo].[Permissions] ([Key], [Description]) VALUES (N'Admin.Clearance', N'Администрация.Распродажа');
                DECLARE @permissionId int = SCOPE_IDENTITY();
                INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
                    SELECT DISTINCT rp.[RoleId], @permissionId FROM [dbo].[RolePermissions] rp
                    JOIN [dbo].[Permissions] p ON p.[Id] = rp.[PermissionId] WHERE p.[Key] = N'Admin';
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ClearanceBonusLines");
        migrationBuilder.DropTable("ClearanceBonusReports");
        migrationBuilder.DropTable("ClearanceProducts");
    }
}
