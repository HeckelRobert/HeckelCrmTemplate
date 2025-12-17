using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds missing Lexoffice columns to Offers with IF NOT EXISTS guards (safe/idempotent).
    /// </summary>
    public partial class AddMissingLexofficeColumnsSafe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add DaysUntilAcceptance if not exists
            migrationBuilder.Sql(@"
IF COL_LENGTH('Offers', 'DaysUntilAcceptance') IS NULL
BEGIN
    ALTER TABLE Offers ADD DaysUntilAcceptance INT NULL;
END
");

            // Add LexofficeCreatedAt if not exists
            migrationBuilder.Sql(@"
IF COL_LENGTH('Offers', 'LexofficeCreatedAt') IS NULL
BEGIN
    ALTER TABLE Offers ADD LexofficeCreatedAt DATETIME2 NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop columns if they exist (idempotent)
            migrationBuilder.Sql(@"
IF COL_LENGTH('Offers', 'DaysUntilAcceptance') IS NOT NULL
BEGIN
    ALTER TABLE Offers DROP COLUMN DaysUntilAcceptance;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Offers', 'LexofficeCreatedAt') IS NOT NULL
BEGIN
    ALTER TABLE Offers DROP COLUMN LexofficeCreatedAt;
END
");
        }
    }
}

