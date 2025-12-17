namespace HeckelCrm.Core.Entities;

public class Offer
{
    public Guid Id { get; set; }
    public Guid QuoteRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public QuoteStatus Status { get; set; }
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
    public BillingStatus BillingStatus { get; set; }
    public string? LexofficeVoucherStatus { get; set; }
    public Guid? ApplicationTypeId { get; set; }
    
    public virtual QuoteRequest QuoteRequest { get; set; } = null!;
    public virtual ApplicationType? ApplicationType { get; set; }
}

public enum QuoteStatus
{
    Created = 0,
    Rejected = 1,
    InProgress = 2
}

