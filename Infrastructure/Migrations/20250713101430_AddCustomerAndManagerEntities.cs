using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAndManagerEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_CustomerLevels_CustomerLevelId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "CustomerLevels");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerLevelId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerLevelId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "Customers",
                newName: "Territory");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Customers",
                newName: "FullName");

			migrationBuilder.DropPrimaryKey(
	         name: "PK_OrderDetails",
	         table: "OrderDetails");

			migrationBuilder.AlterColumn<string>(
				name: "ArticleNumber",
				table: "OrderDetails",
				type: "varchar(50)",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "nvarchar(450)");

			migrationBuilder.AddPrimaryKey(
				name: "PK_OrderDetails",
				table: "OrderDetails",
				columns: new[] { "OrderId", "ArticleNumber" });

			migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractDate",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeMonthlyRepayment",
                table: "Customers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MonthlyRepaymentRate",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlannedPurchaseAmount",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceLevelId",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Restriction",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesManagerId",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalPurchaseAmount",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PriceLevels",
                columns: table => new
                {
                    Level = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Coefficient = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinLimit = table.Column<double>(type: "float", nullable: true),
                    MaxLimit = table.Column<double>(type: "float", nullable: true),
                    Code = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLevels", x => x.Level);
                });

            migrationBuilder.CreateTable(
                name: "SalesManagers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contacts = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesManagers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagerBrands",
                columns: table => new
                {
                    ManagerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SalesPercentage = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerBrands", x => new { x.ManagerId, x.Brand });
                    table.ForeignKey(
                        name: "FK_ManagerBrands_SalesManagers_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "SalesManagers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_ArticleNumber",
                table: "OrderDetails",
                column: "ArticleNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PriceLevelId",
                table: "Customers",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SalesManagerId",
                table: "Customers",
                column: "SalesManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PriceLevels_PriceLevelId",
                table: "Customers",
                column: "PriceLevelId",
                principalTable: "PriceLevels",
                principalColumn: "Level");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SalesManagers_SalesManagerId",
                table: "Customers",
                column: "SalesManagerId",
                principalTable: "SalesManagers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails",
                column: "ArticleNumber",
                principalTable: "Products",
                principalColumn: "ArticleNumber",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PriceLevels_PriceLevelId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_SalesManagers_SalesManagerId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Products_ArticleNumber",
                table: "OrderDetails");

            migrationBuilder.DropTable(
                name: "ManagerBrands");

            migrationBuilder.DropTable(
                name: "PriceLevels");

            migrationBuilder.DropTable(
                name: "SalesManagers");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_ArticleNumber",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PriceLevelId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_SalesManagerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContractDate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ExcludeMonthlyRepayment",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MonthlyRepaymentRate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PlannedPurchaseAmount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PriceLevelId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Restriction",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SalesManagerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TotalPurchaseAmount",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "Territory",
                table: "Customers",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "Customers",
                newName: "LastName");

            //migrationBuilder.AlterColumn<string>(
            //    name: "ArticleNumber",
            //    table: "OrderDetails",
            //    type: "nvarchar(450)",
            //    nullable: false,
            //    oldClrType: typeof(string),
            //    oldType: "varchar(50)");

            migrationBuilder.AddColumn<string>(
                name: "CustomerLevelId",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CustomerLevels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DiscountPercentage = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLevels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerLevelId",
                table: "Customers",
                column: "CustomerLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_CustomerLevels_CustomerLevelId",
                table: "Customers",
                column: "CustomerLevelId",
                principalTable: "CustomerLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
