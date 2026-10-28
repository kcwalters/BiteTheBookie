using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSnapshotToEfCore10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertPickRuns",
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
                    table.PrimaryKey("PK_ExpertPickRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertPickRuns_RunDate",
                table: "ExpertPickRuns",
                column: "RunDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertPickRuns");
        }
    }
}
