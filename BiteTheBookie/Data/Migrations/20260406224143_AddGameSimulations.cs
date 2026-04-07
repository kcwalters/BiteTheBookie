using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSimulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSimulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    League = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AwayTeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeTeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GameDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SimulationContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    GeneratedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSimulations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSimulations_GameId",
                table: "GameSimulations",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSimulations_GeneratedAt",
                table: "GameSimulations",
                column: "GeneratedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSimulations");
        }
    }
}
