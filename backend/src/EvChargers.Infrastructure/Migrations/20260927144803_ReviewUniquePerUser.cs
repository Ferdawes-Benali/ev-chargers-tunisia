using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvChargers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewUniquePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_StationId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_StationId_UserId",
                table: "Reviews",
                columns: new[] { "StationId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_StationId_UserId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_StationId",
                table: "Reviews",
                column: "StationId");
        }
    }
}
