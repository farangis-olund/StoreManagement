using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreExchanges_Stores_StoreCode1",
                table: "StoreExchanges");

            migrationBuilder.DropIndex(
                name: "IX_StoreExchanges_StoreCode1",
                table: "StoreExchanges");

            migrationBuilder.DropColumn(
                name: "StoreCode1",
                table: "StoreExchanges");

            migrationBuilder.AlterColumn<string>(
                name: "StoreCode",
                table: "StoreExchanges",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "StoreExchanges",
                type: "varchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_StoreExchanges_ArticleNumber",
                table: "StoreExchanges",
                column: "ArticleNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StoreExchanges_StoreCode",
                table: "StoreExchanges",
                column: "StoreCode");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreExchanges_Products_ArticleNumber",
                table: "StoreExchanges",
                column: "ArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreExchanges_Stores_StoreCode",
                table: "StoreExchanges",
                column: "StoreCode",
                principalTable: "Stores",
                principalColumn: "StoreCode",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreExchanges_Products_ArticleNumber",
                table: "StoreExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreExchanges_Stores_StoreCode",
                table: "StoreExchanges");

            migrationBuilder.DropIndex(
                name: "IX_StoreExchanges_ArticleNumber",
                table: "StoreExchanges");

            migrationBuilder.DropIndex(
                name: "IX_StoreExchanges_StoreCode",
                table: "StoreExchanges");

            migrationBuilder.AlterColumn<string>(
                name: "StoreCode",
                table: "StoreExchanges",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "StoreExchanges",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)");

            migrationBuilder.AddColumn<string>(
                name: "StoreCode1",
                table: "StoreExchanges",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreExchanges_StoreCode1",
                table: "StoreExchanges",
                column: "StoreCode1");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreExchanges_Stores_StoreCode1",
                table: "StoreExchanges",
                column: "StoreCode1",
                principalTable: "Stores",
                principalColumn: "StoreCode");
        }
    }
}
