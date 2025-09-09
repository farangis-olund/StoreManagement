using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricesIntoProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prices");

            migrationBuilder.AddColumn<decimal>(
                name: "NetPrice",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetailPriceEuro",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServicePriceEuro",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SmallWholesalePrice",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WholesalePrice1Euro",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WholesalePriceEuro",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RetailPriceEuro",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ServicePriceEuro",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SmallWholesalePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WholesalePrice1Euro",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WholesalePriceEuro",
                table: "Products");

            migrationBuilder.CreateTable(
                name: "Prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleNumber = table.Column<string>(type: "varchar(50)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DicountPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DiscountPrice = table.Column<decimal>(type: "money", nullable: true),
                    Price = table.Column<decimal>(type: "money", nullable: false),
                    PriceType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prices_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Prices_Products_ArticleNumber",
                        column: x => x.ArticleNumber,
                        principalTable: "Products",
                        principalColumn: "ArticleNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prices_ArticleNumber",
                table: "Prices",
                column: "ArticleNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Prices_CurrencyCode",
                table: "Prices",
                column: "CurrencyCode");
        }
    }
}
