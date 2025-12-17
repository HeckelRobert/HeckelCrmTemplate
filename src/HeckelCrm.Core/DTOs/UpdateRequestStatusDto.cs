namespace HeckelCrm.Core.DTOs;

public class UpdateRequestStatusDto
{
    public string Status { get; set; } = string.Empty;
    public Guid? SelectedQuoteId { get; set; }
}

