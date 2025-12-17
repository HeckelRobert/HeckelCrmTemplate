using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface IContactService
{
    Task<ContactDto> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default);
    Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ContactDto>> GetAllContactsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ContactDto>> GetContactsByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<ContactDto> UpdateContactAsync(Guid id, CreateContactDto dto, CancellationToken cancellationToken = default);
    Task<ContactDto> UpdateBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, bool isAdmin, CancellationToken cancellationToken = default);
    Task<bool> DeleteContactAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContactDto?> CreateLexofficeContactAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContactDto> ProcessWebhookContactAsync(WebhookContactDto dto, CancellationToken cancellationToken = default);
}

