using Microsoft.EntityFrameworkCore.Migrations;

namespace CiellosAzureDashboard.Migrations
{
    public partial class AddAPIKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apiKeyString",
                table: "Settings",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "apiKeyString",
                table: "Settings");
        }
    }
}
