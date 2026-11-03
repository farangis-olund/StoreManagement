using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DatabaseContext))]
    [Migration("20260715001000_FixCustomerLimitColumnTypes")]
    public partial class FixCustomerLimitColumnTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Customers', 'BuyLimit') IS NULL
                BEGIN
                    ALTER TABLE [Customers] ADD [BuyLimit] decimal(18,2) NULL;
                END
                ELSE
                BEGIN
                    ALTER TABLE [Customers] ALTER COLUMN [BuyLimit] decimal(18,2) NULL;
                END

                IF COL_LENGTH('Customers', 'UnpaidPaymentLimit') IS NULL
                BEGIN
                    ALTER TABLE [Customers] ADD [UnpaidPaymentLimit] decimal(18,2) NULL;
                END
                ELSE
                BEGIN
                    ALTER TABLE [Customers] ALTER COLUMN [UnpaidPaymentLimit] decimal(18,2) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Customers', 'BuyLimit') IS NOT NULL
                BEGIN
                    ALTER TABLE [Customers] DROP COLUMN [BuyLimit];
                END
                """);
        }
    }
}
