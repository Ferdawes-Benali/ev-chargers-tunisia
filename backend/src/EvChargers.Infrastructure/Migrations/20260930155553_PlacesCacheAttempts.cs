using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvChargers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlacesCacheAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "PlacesCache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshStartedAt",
                table: "PlacesCache",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "PlacesCache");

            migrationBuilder.DropColumn(
                name: "RefreshStartedAt",
                table: "PlacesCache");
        }
    }
}
