using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureLexofficeColumnsExist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure DaysUntilAcceptance column exists
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Offers]', 'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Offers', 'DaysUntilAcceptance') IS NULL
                    BEGIN
                        ALTER TABLE [Offers] ADD [DaysUntilAcceptance] INT NULL;
                    END
                END
            ");

            // Ensure LexofficeCreatedAt column exists
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Offers]', 'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Offers', 'LexofficeCreatedAt') IS NULL
                    BEGIN
                        ALTER TABLE [Offers] ADD [LexofficeCreatedAt] DATETIME2 NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop columns if they exist (idempotent)
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Offers]', 'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Offers', 'DaysUntilAcceptance') IS NOT NULL
                    BEGIN
                        ALTER TABLE [Offers] DROP COLUMN [DaysUntilAcceptance];
                    END
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Offers]', 'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Offers', 'LexofficeCreatedAt') IS NOT NULL
                    BEGIN
                        ALTER TABLE [Offers] DROP COLUMN [LexofficeCreatedAt];
                    END
                END
            ");
        }
    }
}
