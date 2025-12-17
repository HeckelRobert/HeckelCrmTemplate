namespace HeckelCrm.Core.Interfaces;

public interface IQuoteRequestRepository : IRepository<Entities.QuoteRequest>
{
    Task<IEnumerable<Entities.QuoteRequest>> GetByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default);
}

