using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface IApplicationTypeService
{
    Task<ApplicationTypeDto> CreateApplicationTypeAsync(CreateApplicationTypeDto dto, CancellationToken cancellationToken = default);
    Task<ApplicationTypeDto?> GetApplicationTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApplicationTypeDto>> GetAllApplicationTypesAsync(CancellationToken cancellationToken = default);
    Task<ApplicationTypeDto> UpdateApplicationTypeAsync(Guid id, CreateApplicationTypeDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteApplicationTypeAsync(Guid id, CancellationToken cancellationToken = default);
}

