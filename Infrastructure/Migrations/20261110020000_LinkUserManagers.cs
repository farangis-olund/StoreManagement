using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261110020000_LinkUserManagers")]
public sealed class LinkUserManagers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Address", "Users", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<bool>("IsManager", "Users", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("UserId", "SalesManagers", type: "int", nullable: true);
        migrationBuilder.CreateIndex("IX_SalesManagers_UserId", "SalesManagers", "UserId", unique: true, filter: "[UserId] IS NOT NULL");
        migrationBuilder.AddForeignKey(name: "FK_SalesManagers_Users_UserId", table: "SalesManagers", column: "UserId",
            principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_SalesManagers_Users_UserId", "SalesManagers");
        migrationBuilder.DropIndex("IX_SalesManagers_UserId", "SalesManagers");
        migrationBuilder.DropColumn("UserId", "SalesManagers");
        migrationBuilder.DropColumn("IsManager", "Users");
        migrationBuilder.DropColumn("Address", "Users");
    }
}
