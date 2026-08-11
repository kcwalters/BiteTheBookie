using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    YouTubeId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVideos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVideos_IsFeatured",
                table: "SiteVideos",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVideos_IsPublished",
                table: "SiteVideos",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVideos_SortOrder",
                table: "SiteVideos",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteVideos");
        }
    }
}
