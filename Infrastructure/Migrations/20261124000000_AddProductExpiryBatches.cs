using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261124000000_AddProductExpiryBatches")]
public sealed class AddProductExpiryBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ProductExpiryBatches", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            ArticleNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, unicode: false, nullable: false),
            ManufacturedOn = table.Column<DateTime>(type: "date", nullable: false),
            ExpiresOn = table.Column<DateTime>(type: "date", nullable: false),
            WarehousePlace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ProductExpiryBatches", x => x.Id);
            table.CheckConstraint("CK_ProductExpiryBatches_Dates", "[ManufacturedOn] <= [ExpiresOn]");
            table.ForeignKey("FK_ProductExpiryBatches_Products_ArticleNumber", x => x.ArticleNumber,
                "Products", "ArticleNumber", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_ProductExpiryBatches_ArticleNumber_ManufacturedOn_ExpiresOn_WarehousePlace", "ProductExpiryBatches",
            new[] { "ArticleNumber", "ManufacturedOn", "ExpiresOn", "WarehousePlace" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProductExpiryBatches_IsActive_ExpiresOn", "ProductExpiryBatches", new[] { "IsActive", "ExpiresOn" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ProductExpiryBatches");
}
