using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAngebotToOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old foreign key from QuoteRequests
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteRequests_Angebote_SelectedQuoteId",
                table: "QuoteRequests");

            // Drop foreign keys from Angebote table
            migrationBuilder.DropForeignKey(
                name: "FK_Angebote_ApplicationTypes_ApplicationTypeId",
                table: "Angebote");

            migrationBuilder.DropForeignKey(
                name: "FK_Angebote_QuoteRequests_QuoteRequestId",
                table: "Angebote");

            // Rename the table from Angebote to Offers
            migrationBuilder.RenameTable(
                name: "Angebote",
                newName: "Offers",
                newSchema: null);

            // Rename indexes
            migrationBuilder.RenameIndex(
                name: "IX_Angebote_ApplicationTypeId",
                table: "Offers",
                newName: "IX_Offers_ApplicationTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Angebote_LexofficeQuoteId",
                table: "Offers",
                newName: "IX_Offers_LexofficeQuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_Angebote_QuoteRequestId",
                table: "Offers",
                newName: "IX_Offers_QuoteRequestId");

            // Recreate foreign keys with new names
            migrationBuilder.AddForeignKey(
                name: "FK_Offers_ApplicationTypes_ApplicationTypeId",
                table: "Offers",
                column: "ApplicationTypeId",
                principalTable: "ApplicationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Offers_QuoteRequests_QuoteRequestId",
                table: "Offers",
                column: "QuoteRequestId",
                principalTable: "QuoteRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Recreate the foreign key from QuoteRequests to Offers
            migrationBuilder.AddForeignKey(
                name: "FK_QuoteRequests_Offers_SelectedQuoteId",
                table: "QuoteRequests",
                column: "SelectedQuoteId",
                principalTable: "Offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteRequests_Offers_SelectedQuoteId",
                table: "QuoteRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Offers_ApplicationTypes_ApplicationTypeId",
                table: "Offers");

            migrationBuilder.DropForeignKey(
                name: "FK_Offers_QuoteRequests_QuoteRequestId",
                table: "Offers");

            // Rename indexes back
            migrationBuilder.RenameIndex(
                name: "IX_Offers_ApplicationTypeId",
                table: "Offers",
                newName: "IX_Angebote_ApplicationTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Offers_LexofficeQuoteId",
                table: "Offers",
                newName: "IX_Angebote_LexofficeQuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_Offers_QuoteRequestId",
                table: "Offers",
                newName: "IX_Angebote_QuoteRequestId");

            // Rename the table back from Offers to Angebote
            migrationBuilder.RenameTable(
                name: "Offers",
                newName: "Angebote",
                newSchema: null);

            // Recreate foreign keys with old names
            migrationBuilder.AddForeignKey(
                name: "FK_Angebote_ApplicationTypes_ApplicationTypeId",
                table: "Angebote",
                column: "ApplicationTypeId",
                principalTable: "ApplicationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Angebote_QuoteRequests_QuoteRequestId",
                table: "Angebote",
                column: "QuoteRequestId",
                principalTable: "QuoteRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Recreate the foreign key from QuoteRequests to Angebote
            migrationBuilder.AddForeignKey(
                name: "FK_QuoteRequests_Angebote_SelectedQuoteId",
                table: "QuoteRequests",
                column: "SelectedQuoteId",
                principalTable: "Angebote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
