namespace HeckelCrm.Core.DTOs;

public class WebhookLeadDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string PartnerId { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public bool PrivacyPolicyAccepted { get; set; }
    public DateTime PrivacyPolicyAcceptedAt { get; set; }
    public string? WebhookSecret { get; set; } // For webhook authentication
}

