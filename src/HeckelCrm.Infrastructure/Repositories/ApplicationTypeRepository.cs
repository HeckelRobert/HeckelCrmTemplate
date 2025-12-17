using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;

namespace HeckelCrm.Infrastructure.Repositories;

public class ApplicationTypeRepository : Repository<ApplicationType>, IApplicationTypeRepository
{
    public ApplicationTypeRepository(ApplicationDbContext context) : base(context)
    {
    }
}

