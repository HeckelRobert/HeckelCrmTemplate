namespace HeckelCrm.Core.DTOs;

public class CreateQuoteRequestDto
{
    public Guid ContactId { get; set; }
    public string? Requirements { get; set; }
}

