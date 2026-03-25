using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanCascadeStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Groups_GroupId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnDetails_Products_ProductArticleNumber",
                table: "ReturnDetails");

            migrationBuilder.DropIndex(
                name: "IX_ReturnDetails_ProductArticleNumber",
                table: "ReturnDetails");

            migrationBuilder.DropColumn(
                name: "ProductArticleNumber",
                table: "ReturnDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "ReturnDetails",
                type: "varchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDetails_ArticleNumber",
                table: "ReturnDetails",
                column: "ArticleNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails",
                column: "ArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Groups_GroupId",
                table: "Products",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnDetails_Products_ArticleNumber",
                table: "ReturnDetails",
                column: "ArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Groups_GroupId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnDetails_Products_ArticleNumber",
                table: "ReturnDetails");

            migrationBuilder.DropIndex(
                name: "IX_ReturnDetails_ArticleNumber",
                table: "ReturnDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "ReturnDetails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)");

            migrationBuilder.AddColumn<string>(
                name: "ProductArticleNumber",
                table: "ReturnDetails",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDetails_ProductArticleNumber",
                table: "ReturnDetails",
                column: "ProductArticleNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails",
                column: "ArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Customers_CustomerId",
                table: "Payments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Groups_GroupId",
                table: "Products",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnDetails_Products_ProductArticleNumber",
                table: "ReturnDetails",
                column: "ProductArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber");
        }
    }
}
