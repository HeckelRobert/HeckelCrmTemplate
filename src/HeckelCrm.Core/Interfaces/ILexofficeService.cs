namespace HeckelCrm.Core.Interfaces;

public interface ILexofficeService
{
    /// <summary>
    /// Gets the configured API key. Returns empty string if not configured.
    /// </summary>
    string GetApiKey();

    Task<string> CreateContactAsync(ContactData contact, CancellationToken cancellationToken = default);
    Task<bool> UpdateContactAsync(string contactId, ContactData contact, CancellationToken cancellationToken = default);
    Task<bool> ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default);
    Task<LexofficeContactInfo?> GetContactAsync(string contactId, CancellationToken cancellationToken = default);
    Task<string> CreateQuoteAsync(QuoteData quote, CancellationToken cancellationToken = default);
    Task<QuoteInfo?> GetQuoteAsync(string quoteId, CancellationToken cancellationToken = default);
    Task<string?> GetQuoteLinkAsync(string quoteId, CancellationToken cancellationToken = default);
    Task<IEnumerable<QuoteInfo>> GetQuotesByContactIdAsync(string contactId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ArticleInfo>> GetArticlesAsync(CancellationToken cancellationToken = default);
}

public record ContactData(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    // Company Information
    string? CompanyName,
    string? CompanyTaxNumber,
    string? CompanyVatRegistrationId,
    bool CompanyAllowTaxFreeInvoices,
    // Billing Address
    string? BillingStreet,
    string? BillingZip,
    string? BillingCity,
    string? BillingCountryCode,
    string? BillingSupplement,
    // Shipping Address
    string? ShippingStreet,
    string? ShippingZip,
    string? ShippingCity,
    string? ShippingCountryCode,
    string? ShippingSupplement,
    // Email Addresses
    string? EmailBusiness,
    string? EmailOffice,
    string? EmailPrivate,
    string? EmailOther,
    // Phone Numbers
    string? PhoneBusiness,
    string? PhoneOffice,
    string? PhoneMobile,
    string? PhonePrivate,
    string? PhoneFax,
    string? PhoneOther,
    // Salutation
    string? Salutation,
    // Partner ID for note field
    string? PartnerId
);

public record QuoteData(
    string ContactId,    
    string Currency,
    DateTime ValidUntil,
    List<QuoteLineItem>? LineItems = null
);

public record QuoteLineItem(
    string? ArticleId,
    string Name,
    string Type,
    string? Description,
    decimal Quantity,
    string UnitName,
    decimal UnitPrice,
    int TaxRatePercentage,
    int Days
);

public record ArticleInfo(
    string Id,
    string Number,
    string Name,
    string? Description,
    decimal? UnitPrice,
    string? UnitName,
    int? TaxRatePercentage
);

public record QuoteInfo(
    string Id,
    string? QuoteNumber,
    string? Link,
    DateTime CreatedAt,
    DateTime? ValidUntil,
    string Status,
    string? VoucherStatus = null,
    List<QuoteLineItemInfo>? LineItems = null
);

public record QuoteLineItemInfo(
    string? ArticleId,
    string? Name,
    string? Description,
    decimal Quantity,
    string? UnitName,
    decimal? UnitPrice,
    int? TaxRatePercentage
);

public record LexofficeContactInfo(
    string Id,
    bool Archived
);

