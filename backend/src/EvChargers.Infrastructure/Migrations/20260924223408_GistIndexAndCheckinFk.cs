using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvChargers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GistIndexAndCheckinFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stations_Location",
                table: "Stations");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_Location",
                table: "Stations",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Checkins_StationId",
                table: "Checkins",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Checkins_Stations_StationId",
                table: "Checkins",
                column: "StationId",
                principalTable: "Stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checkins_Stations_StationId",
                table: "Checkins");

            migrationBuilder.DropIndex(
                name: "IX_Stations_Location",
                table: "Stations");

            migrationBuilder.DropIndex(
                name: "IX_Checkins_StationId",
                table: "Checkins");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_Location",
                table: "Stations",
                column: "Location");
        }
    }
}
