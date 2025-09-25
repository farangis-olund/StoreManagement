using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class returnReasonTableIsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Returns");

            migrationBuilder.AddColumn<int>(
                name: "ReturnReasonId",
                table: "Returns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReturnReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Returns_ReturnReasonId",
                table: "Returns",
                column: "ReturnReasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Returns_ReturnReasons_ReturnReasonId",
                table: "Returns",
                column: "ReturnReasonId",
                principalTable: "ReturnReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Returns_ReturnReasons_ReturnReasonId",
                table: "Returns");

            migrationBuilder.DropTable(
                name: "ReturnReasons");

            migrationBuilder.DropIndex(
                name: "IX_Returns_ReturnReasonId",
                table: "Returns");

            migrationBuilder.DropColumn(
                name: "ReturnReasonId",
                table: "Returns");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Returns",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
