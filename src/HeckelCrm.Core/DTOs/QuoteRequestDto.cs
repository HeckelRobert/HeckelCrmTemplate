namespace HeckelCrm.Core.DTOs;

public class QuoteRequestDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? Requirements { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? SelectedQuoteId { get; set; }
    public string? SelectedQuoteTitle { get; set; }
    public int AngeboteCount { get; set; }
}

