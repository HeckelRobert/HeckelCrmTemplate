using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLexofficeVoucherStatusToAngebot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'LexofficeVoucherStatus')
                    BEGIN
                        ALTER TABLE [Angebote] ADD [LexofficeVoucherStatus] nvarchar(max) NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LexofficeVoucherStatus",
                table: "Angebote");
        }
    }
}
