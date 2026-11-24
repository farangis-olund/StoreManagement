using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261124010000_AddExpiryBatchQuantities")]
public sealed class AddExpiryBatchQuantities : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.DropIndex("IX_ProductExpiryBatches_IsActive_ExpiresOn", "ProductExpiryBatches");
        m.DropColumn("IsActive", "ProductExpiryBatches");
        // Existing rows have no reliable per-batch count; do not duplicate the article's total stock into every batch.
        m.AddColumn<int>("Quantity", "ProductExpiryBatches", type: "int", nullable: false, defaultValue: 0);
        m.AddCheckConstraint("CK_ProductExpiryBatches_Quantity", "ProductExpiryBatches", "[Quantity] >= 0");
        m.CreateIndex("IX_ProductExpiryBatches_Quantity_ExpiresOn", "ProductExpiryBatches", new[] { "Quantity", "ExpiresOn" });
        m.AddColumn<string>("WarehousePlaceSnapshot", "OrderDetails", type: "nvarchar(max)", nullable: true);
        m.AddColumn<string>("SourceOrderId", "ReturnDetails", type: "nvarchar(450)", maxLength: 450, nullable: true);
        m.CreateTable("OrderExpiryAllocations", columns: t => new
        {
            Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            OrderId = t.Column<string>(type: "nvarchar(450)", nullable: false),
            ArticleNumber = t.Column<string>(type: "varchar(50)", nullable: false),
            BatchId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Quantity = t.Column<int>(type: "int", nullable: false),
            ReturnedQuantity = t.Column<int>(type: "int", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_OrderExpiryAllocations", x => x.Id);
            t.CheckConstraint("CK_OrderExpiryAllocations_Quantity", "[Quantity] > 0 AND [ReturnedQuantity] >= 0 AND [ReturnedQuantity] <= [Quantity]");
            t.ForeignKey("FK_OrderExpiryAllocations_OrderDetails_OrderId_ArticleNumber", x => new { x.OrderId, x.ArticleNumber }, "OrderDetails", new[] { "OrderId", "ArticleNumber" }, onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_OrderExpiryAllocations_ProductExpiryBatches_BatchId", x => x.BatchId, "ProductExpiryBatches", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_OrderExpiryAllocations_BatchId", "OrderExpiryAllocations", "BatchId");
        m.CreateIndex("IX_OrderExpiryAllocations_OrderId_ArticleNumber_BatchId", "OrderExpiryAllocations", new[] { "OrderId", "ArticleNumber", "BatchId" }, unique: true);
        m.CreateTable("ReturnExpiryAllocations", columns: t => new
        {
            Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            ReturnDetailId = t.Column<int>(type: "int", nullable: false),
            SaleAllocationId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Quantity = t.Column<int>(type: "int", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_ReturnExpiryAllocations", x => x.Id);
            t.CheckConstraint("CK_ReturnExpiryAllocations_Quantity", "[Quantity] > 0");
            t.ForeignKey("FK_ReturnExpiryAllocations_ReturnDetails_ReturnDetailId", x => x.ReturnDetailId, "ReturnDetails", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_ReturnExpiryAllocations_OrderExpiryAllocations_SaleAllocationId", x => x.SaleAllocationId, "OrderExpiryAllocations", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_ReturnExpiryAllocations_ReturnDetailId", "ReturnExpiryAllocations", "ReturnDetailId");
        m.CreateIndex("IX_ReturnExpiryAllocations_SaleAllocationId", "ReturnExpiryAllocations", "SaleAllocationId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("ReturnExpiryAllocations"); m.DropTable("OrderExpiryAllocations");
        m.DropColumn("SourceOrderId", "ReturnDetails"); m.DropColumn("WarehousePlaceSnapshot", "OrderDetails");
        m.DropIndex("IX_ProductExpiryBatches_Quantity_ExpiresOn", "ProductExpiryBatches");
        m.DropCheckConstraint("CK_ProductExpiryBatches_Quantity", "ProductExpiryBatches");
        m.DropColumn("Quantity", "ProductExpiryBatches");
        m.AddColumn<bool>("IsActive", "ProductExpiryBatches", type: "bit", nullable: false, defaultValue: true);
        m.CreateIndex("IX_ProductExpiryBatches_IsActive_ExpiresOn", "ProductExpiryBatches", new[] { "IsActive", "ExpiresOn" });
    }
}
