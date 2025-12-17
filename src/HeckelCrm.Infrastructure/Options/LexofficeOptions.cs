namespace HeckelCrm.Infrastructure.Options;

public class LexofficeOptions
{
    public const string SectionName = "Lexoffice";
    
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.lexware.io/v1";
}

