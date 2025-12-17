namespace HeckelCrm.Core.Interfaces;

public interface IAdminSettingsRepository
{
    Task<Entities.AdminSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<Entities.AdminSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(Entities.AdminSettings settings, CancellationToken cancellationToken = default);
}

