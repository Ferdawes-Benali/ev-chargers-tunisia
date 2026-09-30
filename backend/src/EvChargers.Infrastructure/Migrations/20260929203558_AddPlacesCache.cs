using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvChargers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacesCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlacesCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacesJson = table.Column<string>(type: "jsonb", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacesCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacesCache_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlacesCache_StationId",
                table: "PlacesCache",
                column: "StationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlacesCache");
        }
    }
}
