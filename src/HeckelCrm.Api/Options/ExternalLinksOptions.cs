namespace HeckelCrm.Api.Options;

public class ExternalLinksOptions
{
    public const string SectionName = "ExternalLinks";
    
    public string PrivacyPolicyUrl { get; set; } = string.Empty;
    public string TermsUrl { get; set; } = string.Empty;
    public string ImprintUrl { get; set; } = string.Empty;
}

