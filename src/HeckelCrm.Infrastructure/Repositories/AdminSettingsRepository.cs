using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class AdminSettingsRepository : IAdminSettingsRepository
{
    private readonly ApplicationDbContext _context;
    private static readonly Guid SingletonId = Guid.Empty;

    public AdminSettingsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminSettings?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AdminSettings
            .FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
    }

    public async Task<AdminSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings == null)
        {
            settings = new AdminSettings
            {
                Id = SingletonId,
                DefaultUnitPrice = null,
                DefaultTaxRatePercentage = null,
                DefaultValidUntilDays = null,
                LexofficeApiKey = null,
                PrivacyPolicyUrl = null,
                TermsUrl = null,
                DataProcessingUrl = null,
                ImprintUrl = null,
                CreatedAt = DateTime.UtcNow
            };
            await _context.AdminSettings.AddAsync(settings, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    public async Task UpdateSettingsAsync(AdminSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Id = SingletonId; // Ensure singleton
        settings.UpdatedAt = DateTime.UtcNow;
        
        var existing = await _context.AdminSettings
            .FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        
        if (existing == null)
        {
            settings.CreatedAt = DateTime.UtcNow;
            await _context.AdminSettings.AddAsync(settings, cancellationToken);
        }
        else
        {
            existing.DefaultUnitPrice = settings.DefaultUnitPrice;
            existing.DefaultTaxRatePercentage = settings.DefaultTaxRatePercentage;
            existing.DefaultValidUntilDays = settings.DefaultValidUntilDays;
            // Only update LexofficeApiKey if a value was provided
            if (!string.IsNullOrWhiteSpace(settings.LexofficeApiKey))
            {
                existing.LexofficeApiKey = settings.LexofficeApiKey;
            }
            existing.PrivacyPolicyUrl = settings.PrivacyPolicyUrl;
            existing.TermsUrl = settings.TermsUrl;
            existing.DataProcessingUrl = settings.DataProcessingUrl;
            existing.ImprintUrl = settings.ImprintUrl;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.AdminSettings.Update(existing);
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}

