using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddingManagerCustomerTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ManagerBrands",
                table: "ManagerBrands");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "ManagerBrands");

            migrationBuilder.AddColumn<int>(
                name: "BrandId",
                table: "ManagerBrands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ManagerBrands",
                table: "ManagerBrands",
                columns: new[] { "ManagerId", "BrandId" });

            migrationBuilder.CreateTable(
                name: "ManagerCustomers",
                columns: table => new
                {
                    ManagerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerCustomers", x => new { x.ManagerId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_ManagerCustomers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManagerCustomers_SalesManagers_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "SalesManagers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagerBrands_BrandId",
                table: "ManagerBrands",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerCustomers_CustomerId",
                table: "ManagerCustomers",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ManagerBrands_Brands_BrandId",
                table: "ManagerBrands",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManagerBrands_Brands_BrandId",
                table: "ManagerBrands");

            migrationBuilder.DropTable(
                name: "ManagerCustomers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ManagerBrands",
                table: "ManagerBrands");

            migrationBuilder.DropIndex(
                name: "IX_ManagerBrands_BrandId",
                table: "ManagerBrands");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "ManagerBrands");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "ManagerBrands",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ManagerBrands",
                table: "ManagerBrands",
                columns: new[] { "ManagerId", "Brand" });
        }
    }
}
