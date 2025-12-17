namespace HeckelCrm.Core.Interfaces;

public interface IOfferRepository : IRepository<Entities.Offer>
{
    Task<IEnumerable<Entities.Offer>> GetByQuoteRequestIdAsync(Guid quoteRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Offer>> GetByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Offer>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<Entities.Offer?> GetByLexofficeQuoteIdAsync(string lexofficeQuoteId, CancellationToken cancellationToken = default);
}

