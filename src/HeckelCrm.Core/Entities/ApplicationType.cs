namespace HeckelCrm.Core.Entities;

public class ApplicationType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

