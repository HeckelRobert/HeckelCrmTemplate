using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface ILeadService
{
    Task<LeadDto> CreateLeadAsync(CreateLeadDto dto, CancellationToken cancellationToken = default);
    Task<LeadDto?> GetLeadByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<LeadDto>> GetAllLeadsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<LeadDto>> GetLeadsByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<LeadDto> UpdateLeadAsync(Guid id, CreateLeadDto dto, CancellationToken cancellationToken = default);
    Task<LeadDto> UpdateLeadStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteLeadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LeadDto> ProcessWebhookLeadAsync(WebhookLeadDto dto, CancellationToken cancellationToken = default);
    Task<LeadDto?> CreateLexofficeContactAsync(Guid id, CancellationToken cancellationToken = default);
}

