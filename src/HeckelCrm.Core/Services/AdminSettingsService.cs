using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class AdminSettingsService : IAdminSettingsService
{
    private readonly IAdminSettingsRepository _repository;
    private readonly ILogger<AdminSettingsService> _logger;

    public AdminSettingsService(
        IAdminSettingsRepository repository,
        ILogger<AdminSettingsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AdminSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetOrCreateSettingsAsync(cancellationToken);
        return MapToDto(settings);
    }

    public async Task<AdminSettingsDto> UpdateSettingsAsync(UpdateAdminSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetOrCreateSettingsAsync(cancellationToken);
        
        settings.DefaultUnitPrice = dto.DefaultUnitPrice;
        settings.DefaultTaxRatePercentage = dto.DefaultTaxRatePercentage;
        settings.DefaultValidUntilDays = dto.DefaultValidUntilDays;
        
        // Only update LexofficeApiKey if a value was provided (not null or empty)
        if (!string.IsNullOrWhiteSpace(dto.LexofficeApiKey))
        {
            settings.LexofficeApiKey = dto.LexofficeApiKey;
        }
        // If empty, keep existing value (handled in repository)
        
        settings.PrivacyPolicyUrl = dto.PrivacyPolicyUrl;
        settings.TermsUrl = dto.TermsUrl;
        settings.DataProcessingUrl = dto.DataProcessingUrl;
        settings.ImprintUrl = dto.ImprintUrl;
        
        await _repository.UpdateSettingsAsync(settings, cancellationToken);
        
        // Reload to get updated values
        var updatedSettings = await _repository.GetSettingsAsync(cancellationToken);
        return MapToDto(updatedSettings ?? settings);
    }

    private static AdminSettingsDto MapToDto(Entities.AdminSettings settings)
    {
        return new AdminSettingsDto
        {
            DefaultUnitPrice = settings.DefaultUnitPrice,
            DefaultTaxRatePercentage = settings.DefaultTaxRatePercentage,
            DefaultValidUntilDays = settings.DefaultValidUntilDays,
            LexofficeApiKey = settings.LexofficeApiKey,
            PrivacyPolicyUrl = settings.PrivacyPolicyUrl,
            TermsUrl = settings.TermsUrl,
            DataProcessingUrl = settings.DataProcessingUrl,
            ImprintUrl = settings.ImprintUrl,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

