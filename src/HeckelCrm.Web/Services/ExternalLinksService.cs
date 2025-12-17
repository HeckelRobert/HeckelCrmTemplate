using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;

namespace HeckelCrm.Web.Services;

public class ExternalLinksService
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<ExternalLinksService> _logger;
    private readonly IConfiguration _configuration;
    private AdminSettingsDto? _cachedSettings;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public ExternalLinksService(
        ApiClient apiClient,
        ILogger<ExternalLinksService> logger,
        IConfiguration configuration)
    {
        _apiClient = apiClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<Options.ExternalLinksOptions> GetExternalLinksAsync(CancellationToken cancellationToken = default)
    {
        // Use cached value if available and not expired
        if (_cachedSettings != null && DateTime.UtcNow < _cacheExpiry)
        {
            return MapToOptions(_cachedSettings);
        }

        try
        {
            var settings = await _apiClient.GetAdminSettingsAsync(cancellationToken);
            if (settings != null)
            {
                _cachedSettings = settings;
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                return MapToOptions(settings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load external links from admin settings, falling back to configuration");
        }

        // Fallback to configuration
        return new Options.ExternalLinksOptions
        {
            PrivacyPolicyUrl = _configuration["ExternalLinks:PrivacyPolicyUrl"] ?? string.Empty,
            TermsUrl = _configuration["ExternalLinks:TermsUrl"] ?? string.Empty,
            DataProcessingUrl = _configuration["ExternalLinks:DataProcessingUrl"] ?? string.Empty
        };
    }

    private static Options.ExternalLinksOptions MapToOptions(AdminSettingsDto settings)
    {
        return new Options.ExternalLinksOptions
        {
            PrivacyPolicyUrl = settings.PrivacyPolicyUrl ?? string.Empty,
            TermsUrl = settings.TermsUrl ?? string.Empty,
            DataProcessingUrl = settings.DataProcessingUrl ?? string.Empty
        };
    }

    public void InvalidateCache()
    {
        _cachedSettings = null;
        _cacheExpiry = DateTime.MinValue;
    }
}

