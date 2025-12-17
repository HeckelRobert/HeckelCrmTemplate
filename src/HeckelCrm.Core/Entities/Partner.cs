namespace HeckelCrm.Core.Entities;

public class Partner
{
    public Guid Id { get; set; }
    public string PartnerId { get; set; } = string.Empty; // Unique identifier for partner
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EntraIdObjectId { get; set; } // Entra ID Object ID
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}

