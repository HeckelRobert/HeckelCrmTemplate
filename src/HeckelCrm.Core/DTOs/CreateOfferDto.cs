namespace HeckelCrm.Core.DTOs;

public class CreateOfferDto
{
    public List<Guid> QuoteRequestIds { get; set; } = new();
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime ValidUntil { get; set; }
    public List<OfferLineItemDto> LineItems { get; set; } = new();
    public Guid? ApplicationTypeId { get; set; }
}

public class OfferLineItemDto
{
    public string ArticleId { get; set; } = null!;
    public string ArticleType { get; set; } = "service";
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string UnitName { get; set; } = "Tage";
    public decimal UnitPrice { get; set; }
    public int TaxRatePercentage { get; set; } = 19;
    public int Days { get; set; } = 1;
}

