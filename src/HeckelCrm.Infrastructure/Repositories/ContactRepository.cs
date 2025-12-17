using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class ContactRepository : Repository<Contact>, IContactRepository
{
    public ContactRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Contact>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Partner)
            .Where(c => c.PartnerId == partnerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Contact?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Partner)
            .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
    }

    public override async Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Partner)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}

