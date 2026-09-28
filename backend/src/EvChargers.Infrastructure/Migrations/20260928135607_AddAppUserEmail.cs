using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvChargers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "AppUsers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "AppUsers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "fr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "AppUsers");
        }
    }
}
