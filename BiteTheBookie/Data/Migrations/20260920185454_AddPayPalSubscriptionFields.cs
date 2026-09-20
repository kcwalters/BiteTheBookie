using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiteTheBookie.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayPalSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('AspNetUsers', 'PayPalSubscriptionId') IS NULL
                    ALTER TABLE [AspNetUsers] ADD [PayPalSubscriptionId] nvarchar(max) NULL;");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AspNetUsers', 'SubscriptionCancelled') IS NULL
                    ALTER TABLE [AspNetUsers] ADD [SubscriptionCancelled] bit NOT NULL CONSTRAINT [DF_AspNetUsers_SubscriptionCancelled] DEFAULT CAST(0 AS bit);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('AspNetUsers', 'PayPalSubscriptionId') IS NOT NULL
                    ALTER TABLE [AspNetUsers] DROP COLUMN [PayPalSubscriptionId];");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AspNetUsers_SubscriptionCancelled')
                    ALTER TABLE [AspNetUsers] DROP CONSTRAINT [DF_AspNetUsers_SubscriptionCancelled];
                IF COL_LENGTH('AspNetUsers', 'SubscriptionCancelled') IS NOT NULL
                    ALTER TABLE [AspNetUsers] DROP COLUMN [SubscriptionCancelled];");
        }
    }
}
