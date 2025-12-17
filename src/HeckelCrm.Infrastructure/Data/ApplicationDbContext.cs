using HeckelCrm.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Partner> Partners { get; set; }
    public DbSet<QuoteRequest> QuoteRequests { get; set; }
    public DbSet<Offer> Offers { get; set; }
    public DbSet<ApplicationType> ApplicationTypes { get; set; }
    public DbSet<AdminSettings> AdminSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Contact configuration
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.PartnerId).HasMaxLength(100);
            entity.Property(e => e.BillingStatus).HasConversion<int>();
            
            // Company fields
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
            entity.Property(e => e.CompanyVatRegistrationId).HasMaxLength(50);
            
            // Address fields
            entity.Property(e => e.BillingStreet).HasMaxLength(200);
            entity.Property(e => e.BillingZip).HasMaxLength(20);
            entity.Property(e => e.BillingCity).HasMaxLength(100);
            entity.Property(e => e.BillingCountryCode).HasMaxLength(2);
            entity.Property(e => e.BillingSupplement).HasMaxLength(200);
            
            entity.Property(e => e.ShippingStreet).HasMaxLength(200);
            entity.Property(e => e.ShippingZip).HasMaxLength(20);
            entity.Property(e => e.ShippingCity).HasMaxLength(100);
            entity.Property(e => e.ShippingCountryCode).HasMaxLength(2);
            entity.Property(e => e.ShippingSupplement).HasMaxLength(200);
            
            // Email and phone fields
            entity.Property(e => e.EmailBusiness).HasMaxLength(320);
            entity.Property(e => e.EmailOffice).HasMaxLength(320);
            entity.Property(e => e.EmailPrivate).HasMaxLength(320);
            entity.Property(e => e.EmailOther).HasMaxLength(320);
            
            entity.Property(e => e.PhoneBusiness).HasMaxLength(50);
            entity.Property(e => e.PhoneOffice).HasMaxLength(50);
            entity.Property(e => e.PhoneMobile).HasMaxLength(50);
            entity.Property(e => e.PhonePrivate).HasMaxLength(50);
            entity.Property(e => e.PhoneFax).HasMaxLength(50);
            entity.Property(e => e.PhoneOther).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(50);
            
            // Salutation
            entity.Property(e => e.Salutation).HasMaxLength(20);
            
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.PartnerId);
            entity.HasIndex(e => e.LexofficeContactId);
            entity.HasOne(e => e.Partner)
                .WithMany(p => p.Contacts)
                .HasForeignKey(e => e.PartnerId)
                .HasPrincipalKey(p => p.PartnerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // QuoteRequest configuration
        modelBuilder.Entity<QuoteRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Requirements).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => e.ContactId);
            entity.HasIndex(e => e.SelectedQuoteId);
            entity.HasOne(e => e.Contact)
                .WithMany(c => c.QuoteRequests)
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SelectedQuote)
                .WithMany()
                .HasForeignKey(e => e.SelectedQuoteId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // Partner configuration
        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PartnerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(e => e.PartnerId).IsUnique();
            entity.HasIndex(e => e.EntraIdObjectId);
        });

        // Offer configuration
        modelBuilder.Entity<Offer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("EUR");
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.BillingStatus).HasConversion<int>();
            entity.Property(e => e.LexofficeQuoteNumber).HasMaxLength(100);
            entity.HasIndex(e => e.QuoteRequestId);
            entity.HasIndex(e => e.LexofficeQuoteId);
            entity.HasOne(e => e.QuoteRequest)
                .WithMany(qr => qr.Offers)
                .HasForeignKey(e => e.QuoteRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ApplicationType)
                .WithMany(at => at.Offers)
                .HasForeignKey(e => e.ApplicationTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // ApplicationType configuration
        modelBuilder.Entity<ApplicationType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name);
        });

        // AdminSettings configuration (Singleton)
        modelBuilder.Entity<AdminSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DefaultUnitPrice).HasPrecision(18, 2).IsRequired(false);
            entity.Property(e => e.DefaultTaxRatePercentage).IsRequired(false);
            entity.Property(e => e.DefaultValidUntilDays).IsRequired(false);
            entity.Property(e => e.LexofficeApiKey).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.PrivacyPolicyUrl).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.TermsUrl).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.DataProcessingUrl).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.ImprintUrl).HasMaxLength(500).IsRequired(false);
            entity.HasIndex(e => e.Id).IsUnique();
        });
    }
}

