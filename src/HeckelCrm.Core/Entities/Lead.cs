namespace HeckelCrm.Core.Entities;

public class Lead
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PartnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public LeadStatus Status { get; set; }
    public string? Notes { get; set; }
    
    // Privacy and Terms Acceptance
    public bool PrivacyPolicyAccepted { get; set; }
    public DateTime PrivacyPolicyAcceptedAt { get; set; }
    public bool TermsAccepted { get; set; }
    public DateTime TermsAcceptedAt { get; set; }
    public bool DataProcessingAccepted { get; set; }
    public DateTime DataProcessingAcceptedAt { get; set; }
    
    // Lexoffice Integration
    public string? LexofficeContactId { get; set; }
    
    // Company Information
    public string? CompanyName { get; set; }
    public string? CompanyTaxNumber { get; set; }
    public string? CompanyVatRegistrationId { get; set; }
    public bool CompanyAllowTaxFreeInvoices { get; set; }
    
    // Billing Address
    public string? BillingStreet { get; set; }
    public string? BillingZip { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingCountryCode { get; set; }
    public string? BillingSupplement { get; set; }
    
    // Shipping Address
    public string? ShippingStreet { get; set; }
    public string? ShippingZip { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingCountryCode { get; set; }
    public string? ShippingSupplement { get; set; }
    
    // Email Addresses
    public string? EmailBusiness { get; set; }
    public string? EmailOffice { get; set; }
    public string? EmailPrivate { get; set; }
    public string? EmailOther { get; set; }
    
    // Phone Numbers
    public string? PhoneBusiness { get; set; }
    public string? PhoneOffice { get; set; }
    public string? PhoneMobile { get; set; }
    public string? PhonePrivate { get; set; }
    public string? PhoneFax { get; set; }
    public string? PhoneOther { get; set; }
    
    // Salutation
    public string? Salutation { get; set; }
    
    // Navigation properties
    public virtual Partner? Partner { get; set; }
    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

public enum LeadStatus
{
    New = 0,
    InBearbeitung = 1,
    OfferCreated = 2,
    Abgelehnt = 3,
    Contacted = 4, // Legacy - kept for backward compatibility
    Qualified = 5, // Legacy - kept for backward compatibility
    Converted = 6, // Legacy - kept for backward compatibility
    Lost = 7 // Legacy - kept for backward compatibility
}

