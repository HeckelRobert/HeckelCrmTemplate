using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDaysToAngebot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'Days')
                    BEGIN
                        ALTER TABLE [Angebote] ADD [Days] int NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Days",
                table: "Angebote");
        }
    }
}
