using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeckelCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLeadToContactAndAddQuoteRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Partners table if it doesn't exist (needed for foreign keys)
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Partners]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Partners] (
                        [Id] uniqueidentifier NOT NULL,
                        [PartnerId] nvarchar(100) NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [Email] nvarchar(320) NOT NULL,
                        [EntraIdObjectId] nvarchar(450) NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_Partners] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_Partners_PartnerId] ON [Partners] ([PartnerId]);
                    CREATE INDEX [IX_Partners_EntraIdObjectId] ON [Partners] ([EntraIdObjectId]);
                END
            ");

            // Create Angebote table if it doesn't exist (basic structure, will be extended by later migrations)
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Angebote] (
                        [Id] uniqueidentifier NOT NULL,
                        [Title] nvarchar(500) NOT NULL,
                        [Description] nvarchar(max) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [Currency] nvarchar(10) NOT NULL DEFAULT 'EUR',
                        [Status] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [ValidUntil] datetime2 NULL,
                        [ClientAcceptedAt] datetime2 NULL,
                        [LexofficeQuoteId] nvarchar(450) NULL,
                        [LexofficeQuoteLink] nvarchar(max) NULL,
                        [LexofficeQuoteNumber] nvarchar(100) NULL,
                        [QuoteRequestId] uniqueidentifier NULL,
                        CONSTRAINT [PK_Angebote] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_Angebote_LexofficeQuoteId] ON [Angebote] ([LexofficeQuoteId]);
                    CREATE INDEX [IX_Angebote_QuoteRequestId] ON [Angebote] ([QuoteRequestId]);
                END
            ");

            // Check if Leads table exists (for existing databases)
            // If it doesn't exist, we're on a fresh database and will create Contacts directly
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Leads]', 'U') IS NOT NULL
                BEGIN
                    -- Drop foreign key if it exists
                    IF OBJECT_ID(N'[FK_Angebote_Leads_LeadId]', 'F') IS NOT NULL
                    BEGIN
                        ALTER TABLE [Angebote] DROP CONSTRAINT [FK_Angebote_Leads_LeadId];
                    END

                    -- Drop Leads table
                    DROP TABLE [Leads];
                END
            ");

            // Rename column in Angebote table if it exists, or add QuoteRequestId if it doesn't
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL
                BEGIN
                    -- Rename LeadId to QuoteRequestId if LeadId column exists
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'LeadId')
                    BEGIN
                        EXEC sp_rename '[Angebote].[LeadId]', 'QuoteRequestId', 'COLUMN';
                    END
                    -- Add QuoteRequestId if neither LeadId nor QuoteRequestId exists (fresh database)
                    ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'QuoteRequestId')
                    BEGIN
                        ALTER TABLE [Angebote] ADD [QuoteRequestId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                        -- Note: The default value is a placeholder. In a real scenario, you'd need to handle this differently
                        -- For now, we'll make it nullable and handle it in application code
                        ALTER TABLE [Angebote] ALTER COLUMN [QuoteRequestId] uniqueidentifier NULL;
                    END

                    -- Rename index if it exists, or create it if it doesn't
                    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Angebote_LeadId' AND object_id = OBJECT_ID(N'[Angebote]'))
                    BEGIN
                        EXEC sp_rename '[Angebote].[IX_Angebote_LeadId]', 'IX_Angebote_QuoteRequestId', 'INDEX';
                    END
                    ELSE IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Angebote_QuoteRequestId' AND object_id = OBJECT_ID(N'[Angebote]'))
                    BEGIN
                        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'QuoteRequestId')
                        BEGIN
                            CREATE INDEX [IX_Angebote_QuoteRequestId] ON [Angebote] ([QuoteRequestId]);
                        END
                    END
                END
            ");

            // Create Contacts table only if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Contacts]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Contacts] (
                        [Id] uniqueidentifier NOT NULL,
                        [FirstName] nvarchar(200) NOT NULL,
                        [LastName] nvarchar(200) NOT NULL,
                        [Email] nvarchar(320) NOT NULL,
                        [Phone] nvarchar(50) NULL,
                        [PartnerId] nvarchar(100) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [BillingStatus] int NOT NULL,
                        [Notes] nvarchar(max) NULL,
                        [PrivacyPolicyAccepted] bit NOT NULL,
                        [PrivacyPolicyAcceptedAt] datetime2 NOT NULL,
                        [TermsAccepted] bit NOT NULL,
                        [TermsAcceptedAt] datetime2 NOT NULL,
                        [DataProcessingAccepted] bit NOT NULL,
                        [DataProcessingAcceptedAt] datetime2 NOT NULL,
                        [LexofficeContactId] nvarchar(450) NULL,
                        [CompanyName] nvarchar(200) NULL,
                        [CompanyTaxNumber] nvarchar(50) NULL,
                        [CompanyVatRegistrationId] nvarchar(50) NULL,
                        [CompanyAllowTaxFreeInvoices] bit NOT NULL,
                        [BillingStreet] nvarchar(200) NULL,
                        [BillingZip] nvarchar(20) NULL,
                        [BillingCity] nvarchar(100) NULL,
                        [BillingCountryCode] nvarchar(2) NULL,
                        [BillingSupplement] nvarchar(200) NULL,
                        [ShippingStreet] nvarchar(200) NULL,
                        [ShippingZip] nvarchar(20) NULL,
                        [ShippingCity] nvarchar(100) NULL,
                        [ShippingCountryCode] nvarchar(2) NULL,
                        [ShippingSupplement] nvarchar(200) NULL,
                        [EmailBusiness] nvarchar(320) NULL,
                        [EmailOffice] nvarchar(320) NULL,
                        [EmailPrivate] nvarchar(320) NULL,
                        [EmailOther] nvarchar(320) NULL,
                        [PhoneBusiness] nvarchar(50) NULL,
                        [PhoneOffice] nvarchar(50) NULL,
                        [PhoneMobile] nvarchar(50) NULL,
                        [PhonePrivate] nvarchar(50) NULL,
                        [PhoneFax] nvarchar(50) NULL,
                        [PhoneOther] nvarchar(50) NULL,
                        [Salutation] nvarchar(20) NULL,
                        CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id])
                    );

                    -- Create indexes
                    CREATE INDEX [IX_Contacts_Email] ON [Contacts] ([Email]);
                    CREATE INDEX [IX_Contacts_LexofficeContactId] ON [Contacts] ([LexofficeContactId]);
                    CREATE INDEX [IX_Contacts_PartnerId] ON [Contacts] ([PartnerId]);

                    -- Add foreign key to Partners if Partners table exists
                    IF OBJECT_ID(N'[Partners]', 'U') IS NOT NULL
                    BEGIN
                        ALTER TABLE [Contacts] ADD CONSTRAINT [FK_Contacts_Partners_PartnerId] 
                            FOREIGN KEY ([PartnerId]) REFERENCES [Partners] ([PartnerId]) ON DELETE NO ACTION;
                    END
                END
            ");

            // Create QuoteRequests table only if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[QuoteRequests]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [QuoteRequests] (
                        [Id] uniqueidentifier NOT NULL,
                        [ContactId] uniqueidentifier NOT NULL,
                        [Requirements] nvarchar(max) NULL,
                        [Status] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [SelectedQuoteId] uniqueidentifier NULL,
                        CONSTRAINT [PK_QuoteRequests] PRIMARY KEY ([Id])
                    );

                    -- Create indexes
                    CREATE INDEX [IX_QuoteRequests_ContactId] ON [QuoteRequests] ([ContactId]);
                    CREATE INDEX [IX_QuoteRequests_SelectedQuoteId] ON [QuoteRequests] ([SelectedQuoteId]);

                    -- Add foreign keys if tables exist
                    IF OBJECT_ID(N'[Contacts]', 'U') IS NOT NULL
                    BEGIN
                        ALTER TABLE [QuoteRequests] ADD CONSTRAINT [FK_QuoteRequests_Contacts_ContactId] 
                            FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION;
                    END

                    IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL
                    BEGIN
                        ALTER TABLE [QuoteRequests] ADD CONSTRAINT [FK_QuoteRequests_Angebote_SelectedQuoteId] 
                            FOREIGN KEY ([SelectedQuoteId]) REFERENCES [Angebote] ([Id]) ON DELETE NO ACTION;
                    END
                END
            ");

            // Add foreign key from Angebote to QuoteRequests if both tables exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Angebote]', 'U') IS NOT NULL AND OBJECT_ID(N'[QuoteRequests]', 'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[FK_Angebote_QuoteRequests_QuoteRequestId]', 'F') IS NULL
                    BEGIN
                        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Angebote]') AND name = 'QuoteRequestId')
                        BEGIN
                            ALTER TABLE [Angebote] ADD CONSTRAINT [FK_Angebote_QuoteRequests_QuoteRequestId] 
                                FOREIGN KEY ([QuoteRequestId]) REFERENCES [QuoteRequests] ([Id]) ON DELETE NO ACTION;
                        END
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Angebote_QuoteRequests_QuoteRequestId",
                table: "Angebote");

            migrationBuilder.DropTable(
                name: "QuoteRequests");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.RenameColumn(
                name: "QuoteRequestId",
                table: "Angebote",
                newName: "LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Angebote_QuoteRequestId",
                table: "Angebote",
                newName: "IX_Angebote_LeadId");

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    BillingStreet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingSupplement = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingZip = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompanyAllowTaxFreeInvoices = table.Column<bool>(type: "bit", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyTaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyVatRegistrationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataProcessingAccepted = table.Column<bool>(type: "bit", nullable: false),
                    DataProcessingAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    EmailBusiness = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailOffice = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailOther = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailPrivate = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LexofficeContactId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhoneBusiness = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhoneFax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhoneMobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhoneOffice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhoneOther = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhonePrivate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrivacyPolicyAccepted = table.Column<bool>(type: "bit", nullable: false),
                    PrivacyPolicyAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Salutation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShippingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ShippingStreet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingSupplement = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingZip = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TermsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    TermsAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "PartnerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Email",
                table: "Leads",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LexofficeContactId",
                table: "Leads",
                column: "LexofficeContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_PartnerId",
                table: "Leads",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Angebote_Leads_LeadId",
                table: "Angebote",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
