using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Interfaces;

public interface IAdminSettingsService
{
    Task<AdminSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<AdminSettingsDto> UpdateSettingsAsync(UpdateAdminSettingsDto dto, CancellationToken cancellationToken = default);
}

