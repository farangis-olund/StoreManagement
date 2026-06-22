using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserColumnToTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "StoreTransferSummaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StoreTransferSummaries_UserId",
                table: "StoreTransferSummaries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreTransferSummaries_Users_UserId",
                table: "StoreTransferSummaries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreTransferSummaries_Users_UserId",
                table: "StoreTransferSummaries");

            migrationBuilder.DropIndex(
                name: "IX_StoreTransferSummaries_UserId",
                table: "StoreTransferSummaries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "StoreTransferSummaries");
        }
    }
}
