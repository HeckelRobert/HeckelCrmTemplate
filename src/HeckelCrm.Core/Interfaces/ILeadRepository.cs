namespace HeckelCrm.Core.Interfaces;

public interface ILeadRepository : IRepository<Entities.Lead>
{
    Task<IEnumerable<Entities.Lead>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<Entities.Lead?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

