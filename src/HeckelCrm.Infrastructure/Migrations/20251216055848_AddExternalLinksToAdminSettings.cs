using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalLinksToAdminSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataProcessingUrl",
                table: "AdminSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImpressumUrl",
                table: "AdminSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyUrl",
                table: "AdminSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsUrl",
                table: "AdminSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataProcessingUrl",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "ImpressumUrl",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyUrl",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "TermsUrl",
                table: "AdminSettings");
        }
    }
}
