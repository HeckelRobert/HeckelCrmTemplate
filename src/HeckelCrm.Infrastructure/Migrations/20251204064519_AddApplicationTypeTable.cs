using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationTypeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create ApplicationTypes table if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[ApplicationTypes]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [ApplicationTypes] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [Description] nvarchar(1000) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_ApplicationTypes] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_ApplicationTypes_Name] ON [ApplicationTypes] ([Name]);
                END
            ");

            // Add ApplicationTypeId column to Angebote if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'ApplicationTypeId')
                    BEGIN
                        ALTER TABLE [Angebote] ADD [ApplicationTypeId] uniqueidentifier NULL;
                        CREATE INDEX [IX_Angebote_ApplicationTypeId] ON [Angebote] ([ApplicationTypeId]);

                        IF OBJECT_ID(N'[ApplicationTypes]', 'U') IS NOT NULL
                        BEGIN
                            ALTER TABLE [Angebote] ADD CONSTRAINT [FK_Angebote_ApplicationTypes_ApplicationTypeId] 
                                FOREIGN KEY ([ApplicationTypeId]) REFERENCES [ApplicationTypes] ([Id]) ON DELETE NO ACTION;
                        END
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Angebote_ApplicationTypes_ApplicationTypeId",
                table: "Angebote");

            migrationBuilder.DropTable(
                name: "ApplicationTypes");

            migrationBuilder.DropIndex(
                name: "IX_Angebote_ApplicationTypeId",
                table: "Angebote");

            migrationBuilder.DropColumn(
                name: "ApplicationTypeId",
                table: "Angebote");
        }
    }
}
