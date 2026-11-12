using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261112000000_AddPaymentReceiptNumbers")]
public class AddPaymentReceiptNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ReceiptNumber", table: "Payments",
            type: "nvarchar(9)", maxLength: 9, nullable: true);

        // Assign distinct public numbers to historical receipts without changing their IDs or relationships.
        migrationBuilder.Sql("""
            WITH Numbered AS (
                SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS [Sequence] FROM Payments
            )
            UPDATE p SET ReceiptNumber = N'ПЛ-' +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] / 60466176 % 36) + 1, 1) +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] / 1679616 % 36) + 1, 1) +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] / 46656 % 36) + 1, 1) +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] / 1296 % 36) + 1, 1) +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] / 36 % 36) + 1, 1) +
                SUBSTRING(N'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', CONVERT(int, n.[Sequence] % 36) + 1, 1)
            FROM Payments p INNER JOIN Numbered n ON n.Id = p.Id;
            """);

        migrationBuilder.CreateIndex(name: "IX_Payments_ReceiptNumber", table: "Payments",
            column: "ReceiptNumber", unique: true, filter: "[ReceiptNumber] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Payments_ReceiptNumber", table: "Payments");
        migrationBuilder.DropColumn(name: "ReceiptNumber", table: "Payments");
    }
}
