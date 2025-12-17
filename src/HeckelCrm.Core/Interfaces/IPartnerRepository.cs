namespace HeckelCrm.Core.Interfaces;

public interface IPartnerRepository : IRepository<Entities.Partner>
{
    Task<Entities.Partner?> GetByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default);
    Task<Entities.Partner?> GetByEntraIdAsync(string entraIdObjectId, CancellationToken cancellationToken = default);
}

