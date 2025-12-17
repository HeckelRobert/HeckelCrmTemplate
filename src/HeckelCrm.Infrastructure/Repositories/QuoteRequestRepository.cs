using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class QuoteRequestRepository : Repository<QuoteRequest>, IQuoteRequestRepository
{
    public QuoteRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<QuoteRequest>> GetByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(qr => qr.Contact)
            .Include(qr => qr.Offers)
            .Include(qr => qr.SelectedQuote)
            .Where(qr => qr.ContactId == contactId)
            .OrderByDescending(qr => qr.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public override async Task<QuoteRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(qr => qr.Contact)
            .Include(qr => qr.Offers)
            .Include(qr => qr.SelectedQuote)
            .FirstOrDefaultAsync(qr => qr.Id == id, cancellationToken);
    }

    public override async Task<IEnumerable<QuoteRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(qr => qr.Contact)
            .Include(qr => qr.Offers)
            .Include(qr => qr.SelectedQuote)
            .OrderByDescending(qr => qr.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

