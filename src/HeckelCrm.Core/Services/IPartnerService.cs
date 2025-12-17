using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface IPartnerService
{
    Task<PartnerDto> CreateOrGetPartnerAsync(CreatePartnerDto dto, CancellationToken cancellationToken = default);
    Task<PartnerDto?> GetPartnerByEntraIdAsync(string entraIdObjectId, CancellationToken cancellationToken = default);
    Task<PartnerDto?> GetPartnerByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerDto>> GetAllPartnersAsync(CancellationToken cancellationToken = default);
    Task<PartnerDto> UpdatePartnerAsync(string partnerId, UpdatePartnerDto dto, CancellationToken cancellationToken = default);
}

