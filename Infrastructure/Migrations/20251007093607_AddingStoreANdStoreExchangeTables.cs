using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddingStoreANdStoreExchangeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreCode);
                });

            migrationBuilder.CreateTable(
                name: "StoreExchanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArticleNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ExchangeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoreCode1 = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreExchanges_Stores_StoreCode1",
                        column: x => x.StoreCode1,
                        principalTable: "Stores",
                        principalColumn: "StoreCode");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreExchanges_StoreCode1",
                table: "StoreExchanges",
                column: "StoreCode1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreExchanges");

            migrationBuilder.DropTable(
                name: "Stores");
        }
    }
}
