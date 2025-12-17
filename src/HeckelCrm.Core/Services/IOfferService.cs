using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface IOfferService
{
    Task<OfferDto> CreateOfferAsync(CreateOfferDto dto, CancellationToken cancellationToken = default);
    Task<OfferDto?> GetOfferByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OfferDto>> GetAllOffersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OfferDto>> GetOffersByQuoteRequestIdAsync(Guid quoteRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OfferDto>> GetOffersByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OfferDto>> GetOffersByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<OfferDto> UpdateOfferAsync(Guid id, CreateOfferDto dto, CancellationToken cancellationToken = default);
    Task<OfferDto> UpdateOfferStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteOfferAsync(Guid id, CancellationToken cancellationToken = default);
    Task SyncOfferWithLexofficeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OfferDto>> LoadOffersFromLexofficeAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<OfferDto> UpdateBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, bool isAdmin, CancellationToken cancellationToken = default);
    Task<int> BatchSyncOpenOffersWithLexofficeAsync(CancellationToken cancellationToken = default);
}

