using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261102000000_AddProductRequests")]
public class AddProductRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                UserName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CustomerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                CustomerName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ArticleNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                IsSubmitted = table.Column<bool>(type: "bit", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Marka = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Model = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Alternative = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductRequests", x => x.Id);
                table.CheckConstraint("CK_ProductRequests_Quantity", "[Quantity] > 0");
                table.CheckConstraint("CK_ProductRequests_Kind", "[Kind] IN (1, 2, 3)");
            });
        migrationBuilder.CreateIndex("IX_ProductRequests_ArticleNumber", "ProductRequests", "ArticleNumber");
        migrationBuilder.CreateIndex("IX_ProductRequests_Kind_IsSubmitted_CreatedAt", "ProductRequests",
            new[] { "Kind", "IsSubmitted", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_ProductRequests_UserId_IsSubmitted", "ProductRequests",
            new[] { "UserId", "IsSubmitted" });
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_Products_RemoveNewArticleRequests]
            ON [dbo].[Products]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                DELETE r
                FROM [dbo].[ProductRequests] r
                INNER JOIN inserted i ON i.ArticleNumber = r.ArticleNumber
                WHERE r.Kind = 3;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Products_RemoveNewArticleRequests];");
        migrationBuilder.DropTable("ProductRequests");
    }
}
