using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class OfferRepository : Repository<Offer>, IOfferRepository
{
    public OfferRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<Offer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Offer>> GetByQuoteRequestIdAsync(Guid quoteRequestId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .Where(a => a.QuoteRequestId == quoteRequestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Offer>> GetByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .Where(a => a.QuoteRequest.ContactId == contactId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Offer>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .Where(a => a.QuoteRequest.Contact.PartnerId == partnerId)
            .ToListAsync(cancellationToken);
    }

    public override async Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Offer?> GetByLexofficeQuoteIdAsync(string lexofficeQuoteId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.QuoteRequest)
            .ThenInclude(qr => qr.Contact)
            .ThenInclude(c => c.Partner)
            .FirstOrDefaultAsync(a => a.LexofficeQuoteId == lexofficeQuoteId, cancellationToken);
    }
}

