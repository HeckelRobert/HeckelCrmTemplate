namespace HeckelCrm.Core.Interfaces;

public interface IContactRepository : IRepository<Entities.Contact>
{
    Task<IEnumerable<Entities.Contact>> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<Entities.Contact?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

