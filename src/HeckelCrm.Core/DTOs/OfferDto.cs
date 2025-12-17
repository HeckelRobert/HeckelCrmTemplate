namespace HeckelCrm.Core.DTOs;

public class OfferDto
{
    public Guid Id { get; set; }
    public Guid QuoteRequestId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? LexofficeQuoteId { get; set; }
    public string? LexofficeQuoteNumber { get; set; }
    public string? LexofficeQuoteLink { get; set; }
    public DateTime? LexofficeCreatedAt { get; set; }
    public DateTime? ClientAcceptedAt { get; set; }
    public int? DaysUntilAcceptance { get; set; }
    public int? Days { get; set; }
    public string BillingStatus { get; set; } = string.Empty;
    public string? LexofficeVoucherStatus { get; set; }
    public List<OfferLineItemDto>? LineItems { get; set; }
    public Guid? ApplicationTypeId { get; set; }
    public string? ApplicationTypeName { get; set; }
}

