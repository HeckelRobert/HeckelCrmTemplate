using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HeckelCrm.Infrastructure.Repositories;

public class LeadRepository : Repository<Lead>, ILeadRepository
{
    public LeadRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Lead>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(l => l.Partner)
            .Where(l => l.PartnerId == partnerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Lead?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(l => l.Partner)
            .FirstOrDefaultAsync(l => l.Email == email, cancellationToken);
    }

    public override async Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(l => l.Partner)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }
}

