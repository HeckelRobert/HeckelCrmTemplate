namespace HeckelCrm.Core.Entities;

public class QuoteRequest
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string? Requirements { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? SelectedQuoteId { get; set; }
    
    public virtual Contact Contact { get; set; } = null!;
    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public virtual Offer? SelectedQuote { get; set; }
}

public enum RequestStatus
{
    New = 0,
    QuoteCreated = 1,
    Rejected = 2
}

