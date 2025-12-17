using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalutationAndOptionalPartnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if Leads table exists before modifying it
            // If the table doesn't exist, the later migration will create Contacts with Salutation
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Leads]', 'U') IS NOT NULL
                BEGIN
                    -- Add Salutation column if it doesn't exist
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Leads]') AND name = 'Salutation')
                    BEGIN
                        ALTER TABLE [Leads] ADD [Salutation] nvarchar(20) NULL;
                    END

                    -- Make PartnerId nullable if it's not already nullable
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Leads]') AND name = 'PartnerId' AND is_nullable = 0)
                    BEGIN
                        ALTER TABLE [Leads] ALTER COLUMN [PartnerId] nvarchar(100) NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Check if Leads table exists before modifying it
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Leads]', 'U') IS NOT NULL
                BEGIN
                    -- Remove Salutation column if it exists
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Leads]') AND name = 'Salutation')
                    BEGIN
                        ALTER TABLE [Leads] DROP COLUMN [Salutation];
                    END
                END
            ");

            // Note: We don't revert PartnerId to NOT NULL as it might break existing data
            // If you need to revert this, you would need to ensure all PartnerId values are non-null first
        }
    }
}
