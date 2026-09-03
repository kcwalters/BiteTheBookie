using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFantasyFootball : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FantasyContests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    League = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SlateKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SalaryCap = table.Column<int>(type: "int", nullable: false),
                    LockTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsScored = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyContests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FantasyEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FantasyContestId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    TotalSalary = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "decimal(8,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyEntries_FantasyContests_FantasyContestId",
                        column: x => x.FantasyContestId,
                        principalTable: "FantasyContests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FantasyContestId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpponentCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GameId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GameTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Salary = table.Column<int>(type: "int", nullable: false),
                    FantasyPoints = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyPlayers_FantasyContests_FantasyContestId",
                        column: x => x.FantasyContestId,
                        principalTable: "FantasyContests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyEntrySlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FantasyEntryId = table.Column<int>(type: "int", nullable: false),
                    SlotLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FantasyPlayerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyEntrySlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyEntrySlots_FantasyEntries_FantasyEntryId",
                        column: x => x.FantasyEntryId,
                        principalTable: "FantasyEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FantasyEntrySlots_FantasyPlayers_FantasyPlayerId",
                        column: x => x.FantasyPlayerId,
                        principalTable: "FantasyPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyContests_League",
                table: "FantasyContests",
                column: "League");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyContests_SlateKey",
                table: "FantasyContests",
                column: "SlateKey");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntries_FantasyContestId_UserId",
                table: "FantasyEntries",
                columns: new[] { "FantasyContestId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntrySlots_FantasyEntryId",
                table: "FantasyEntrySlots",
                column: "FantasyEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntrySlots_FantasyPlayerId",
                table: "FantasyEntrySlots",
                column: "FantasyPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyPlayers_FantasyContestId",
                table: "FantasyPlayers",
                column: "FantasyContestId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyPlayers_Position",
                table: "FantasyPlayers",
                column: "Position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FantasyEntrySlots");

            migrationBuilder.DropTable(
                name: "FantasyEntries");

            migrationBuilder.DropTable(
                name: "FantasyPlayers");

            migrationBuilder.DropTable(
                name: "FantasyContests");
        }
    }
}
