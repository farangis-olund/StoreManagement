using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261029000000_TrackDeliveryPayments")]
public class TrackDeliveryPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CourierId", table: "Payments", type: "nvarchar(450)", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeliveryConfirmed", table: "Payments", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "CustomerPaymentId", table: "CourierPayments", type: "int", nullable: true);
        migrationBuilder.AlterColumn<string>(name: "OrderId", table: "CourierPayments", type: "nvarchar(450)", nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(450)");

        // Preserve the old confirmed state; historical late receipts cannot be distinguished reliably.
        migrationBuilder.Sql("""
            UPDATE p SET p.CourierId = o.CourierId,
                p.IsDeliveryConfirmed = CASE WHEN o.IsPaid = 1 OR o.IsBarter = 1 THEN 1 ELSE 0 END
            FROM Payments p INNER JOIN Orders o ON o.Id = p.OrderId;
            """);
        migrationBuilder.CreateIndex(name: "IX_Payments_CourierId", table: "Payments", column: "CourierId");
        migrationBuilder.CreateIndex(name: "IX_CourierPayments_CustomerPaymentId", table: "CourierPayments",
            column: "CustomerPaymentId", unique: true, filter: "[CustomerPaymentId] IS NOT NULL");
        migrationBuilder.AddForeignKey(name: "FK_Payments_Couriers_CourierId", table: "Payments", column: "CourierId",
            principalTable: "Couriers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CourierPayments_Payments_CustomerPaymentId", table: "CourierPayments", column: "CustomerPaymentId",
            principalTable: "Payments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse to discard standalone settlements when downgrading to an order-only schema.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM CourierPayments WHERE OrderId IS NULL)
                THROW 51000, 'Cannot downgrade while standalone courier settlements exist.', 1;
            """);
        migrationBuilder.DropForeignKey(name: "FK_CourierPayments_Payments_CustomerPaymentId", table: "CourierPayments");
        migrationBuilder.DropForeignKey(name: "FK_Payments_Couriers_CourierId", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_CourierPayments_CustomerPaymentId", table: "CourierPayments");
        migrationBuilder.DropIndex(name: "IX_Payments_CourierId", table: "Payments");
        migrationBuilder.DropColumn(name: "CustomerPaymentId", table: "CourierPayments");
        migrationBuilder.DropColumn(name: "CourierId", table: "Payments");
        migrationBuilder.DropColumn(name: "IsDeliveryConfirmed", table: "Payments");
        migrationBuilder.AlterColumn<string>(name: "OrderId", table: "CourierPayments", type: "nvarchar(450)", nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(450)", oldNullable: true);
    }
}
