using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class ApplicationTypeService : IApplicationTypeService
{
    private readonly IApplicationTypeRepository _repository;
    private readonly ILogger<ApplicationTypeService> _logger;

    public ApplicationTypeService(
        IApplicationTypeRepository repository,
        ILogger<ApplicationTypeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ApplicationTypeDto> CreateApplicationTypeAsync(CreateApplicationTypeDto dto, CancellationToken cancellationToken = default)
    {
        var applicationType = new ApplicationType
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(applicationType, cancellationToken);
        return MapToDto(applicationType);
    }

    public async Task<ApplicationTypeDto?> GetApplicationTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var applicationType = await _repository.GetByIdAsync(id, cancellationToken);
        if (applicationType == null) return null;

        return MapToDto(applicationType);
    }

    public async Task<IEnumerable<ApplicationTypeDto>> GetAllApplicationTypesAsync(CancellationToken cancellationToken = default)
    {
        var applicationTypes = await _repository.GetAllAsync(cancellationToken);
        return applicationTypes.Select(MapToDto).OrderBy(at => at.Name);
    }

    public async Task<ApplicationTypeDto> UpdateApplicationTypeAsync(Guid id, CreateApplicationTypeDto dto, CancellationToken cancellationToken = default)
    {
        var applicationType = await _repository.GetByIdAsync(id, cancellationToken);
        if (applicationType == null)
        {
            throw new InvalidOperationException($"Application type with ID '{id}' not found.");
        }

        applicationType.Name = dto.Name;
        applicationType.Description = dto.Description;
        applicationType.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(applicationType, cancellationToken);
        return MapToDto(applicationType);
    }

    public async Task<bool> DeleteApplicationTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var applicationType = await _repository.GetByIdAsync(id, cancellationToken);
        if (applicationType == null) return false;

        // Check if any angebote are using this application type
        var hasAngebote = applicationType.Offers.Any();
        if (hasAngebote)
        {
            throw new InvalidOperationException($"Cannot delete application type '{applicationType.Name}' because it is used by one or more offers.");
        }

        await _repository.DeleteAsync(applicationType, cancellationToken);
        return true;
    }

    private static ApplicationTypeDto MapToDto(ApplicationType applicationType)
    {
        return new ApplicationTypeDto
        {
            Id = applicationType.Id,
            Name = applicationType.Name,
            Description = applicationType.Description,
            CreatedAt = applicationType.CreatedAt,
            UpdatedAt = applicationType.UpdatedAt
        };
    }
}

