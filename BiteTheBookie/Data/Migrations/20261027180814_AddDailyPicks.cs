using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPickRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GamesProcessed = table.Column<int>(type: "int", nullable: false),
                    PicksGenerated = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPickRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    League = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AwayTeamName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HomeTeamName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GameTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GameDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Week = table.Column<int>(type: "int", nullable: true),
                    PickType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PickSelection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    PredictedAwayScore = table.Column<int>(type: "int", nullable: true),
                    PredictedHomeScore = table.Column<int>(type: "int", nullable: true),
                    Analysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPickRuns_RunDate",
                table: "DailyPickRuns",
                column: "RunDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPicks_GameDate",
                table: "DailyPicks",
                column: "GameDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPicks_League_GameId",
                table: "DailyPicks",
                columns: new[] { "League", "GameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPickRuns");

            migrationBuilder.DropTable(
                name: "DailyPicks");
        }
    }
}
