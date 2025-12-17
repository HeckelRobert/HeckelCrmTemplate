namespace HeckelCrm.Core.DTOs;

public class PartnerDto
{
    public Guid Id { get; set; }
    public string PartnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EntraIdObjectId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

