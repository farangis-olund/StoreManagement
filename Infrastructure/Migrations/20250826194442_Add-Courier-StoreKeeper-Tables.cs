using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourierStoreKeeperTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourierId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorekeeperId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Couriers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Vehicle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Couriers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Storekeepers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storekeepers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CourierId",
                table: "Orders",
                column: "CourierId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StorekeeperId",
                table: "Orders",
                column: "StorekeeperId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Couriers_CourierId",
                table: "Orders",
                column: "CourierId",
                principalTable: "Couriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Storekeepers_StorekeeperId",
                table: "Orders",
                column: "StorekeeperId",
                principalTable: "Storekeepers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Couriers_CourierId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Storekeepers_StorekeeperId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Couriers");

            migrationBuilder.DropTable(
                name: "Storekeepers");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CourierId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StorekeeperId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CourierId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StorekeeperId",
                table: "Orders");
        }
    }
}
