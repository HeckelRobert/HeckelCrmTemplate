using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class PartnerRepository : Repository<Partner>, IPartnerRepository
{
    public PartnerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Partner?> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Contacts)
            .FirstOrDefaultAsync(p => p.PartnerId == partnerId, cancellationToken);
    }

    public async Task<Partner?> GetByEntraIdAsync(string entraIdObjectId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Contacts)
            .FirstOrDefaultAsync(p => p.EntraIdObjectId == entraIdObjectId, cancellationToken);
    }
}

