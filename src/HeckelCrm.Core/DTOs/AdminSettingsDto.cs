namespace HeckelCrm.Core.DTOs;

public class AdminSettingsDto
{
    public decimal? DefaultUnitPrice { get; set; }
    public int? DefaultTaxRatePercentage { get; set; }
    public int? DefaultValidUntilDays { get; set; }
    public string? LexofficeApiKey { get; set; }
    public string? PrivacyPolicyUrl { get; set; }
    public string? TermsUrl { get; set; }
    public string? DataProcessingUrl { get; set; }
    public string? ImprintUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateAdminSettingsDto
{
    public decimal DefaultUnitPrice { get; set; }
    public int DefaultTaxRatePercentage { get; set; }
    public int DefaultValidUntilDays { get; set; }
    public string? LexofficeApiKey { get; set; }
    public string? PrivacyPolicyUrl { get; set; }
    public string? TermsUrl { get; set; }
    public string? DataProcessingUrl { get; set; }
    public string? ImprintUrl { get; set; }
}

