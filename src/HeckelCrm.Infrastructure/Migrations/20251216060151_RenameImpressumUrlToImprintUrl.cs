using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameImpressumUrlToImprintUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImpressumUrl",
                table: "AdminSettings",
                newName: "ImprintUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImprintUrl",
                table: "AdminSettings",
                newName: "ImpressumUrl");
        }
    }
}
