using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class PartnerService : IPartnerService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly ILogger<PartnerService> _logger;

    public PartnerService(
        IPartnerRepository partnerRepository,
        ILogger<PartnerService> logger)
    {
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    public async Task<PartnerDto> CreateOrGetPartnerAsync(CreatePartnerDto dto, CancellationToken cancellationToken = default)
    {
        // Check if partner already exists by EntraId
        var existingPartner = await _partnerRepository.GetByEntraIdAsync(dto.EntraIdObjectId, cancellationToken);
        if (existingPartner != null)
        {
            return MapToDto(existingPartner);
        }

        // Check if PartnerId is already taken
        var partnerWithId = await _partnerRepository.GetByPartnerIdAsync(dto.PartnerId, cancellationToken);
        if (partnerWithId != null)
        {
            throw new InvalidOperationException($"Partner-ID '{dto.PartnerId}' is already taken.");
        }

        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            PartnerId = dto.PartnerId,
            Name = dto.Name,
            Email = dto.Email,
            EntraIdObjectId = dto.EntraIdObjectId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createdPartner = await _partnerRepository.AddAsync(partner, cancellationToken);
        _logger.LogInformation("Created new partner: {PartnerId} ({Name})", createdPartner.PartnerId, createdPartner.Name);

        return MapToDto(createdPartner);
    }

    public async Task<PartnerDto?> GetPartnerByEntraIdAsync(string entraIdObjectId, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByEntraIdAsync(entraIdObjectId, cancellationToken);
        return partner != null ? MapToDto(partner) : null;
    }

    public async Task<PartnerDto?> GetPartnerByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        return partner != null ? MapToDto(partner) : null;
    }

    public async Task<IEnumerable<PartnerDto>> GetAllPartnersAsync(CancellationToken cancellationToken = default)
    {
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        return partners.Select(MapToDto);
    }

    public async Task<PartnerDto> UpdatePartnerAsync(string partnerId, UpdatePartnerDto dto, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        if (partner == null)
        {
            throw new InvalidOperationException($"Partner with ID '{partnerId}' not found.");
        }

        // Check if new PartnerId is already taken (if changed)
        if (dto.PartnerId != partnerId)
        {
            var existingPartner = await _partnerRepository.GetByPartnerIdAsync(dto.PartnerId, cancellationToken);
            if (existingPartner != null)
            {
                throw new InvalidOperationException($"Partner-ID '{dto.PartnerId}' is already taken.");
            }
        }

        partner.PartnerId = dto.PartnerId;
        partner.Name = dto.Name;
        partner.Email = dto.Email;
        partner.UpdatedAt = DateTime.UtcNow;

        await _partnerRepository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated partner: {PartnerId} ({Name})", partner.PartnerId, partner.Name);

        return MapToDto(partner);
    }

    private static PartnerDto MapToDto(Partner partner)
    {
        return new PartnerDto
        {
            Id = partner.Id,
            PartnerId = partner.PartnerId,
            Name = partner.Name,
            Email = partner.Email,
            EntraIdObjectId = partner.EntraIdObjectId,
            IsActive = partner.IsActive,
            CreatedAt = partner.CreatedAt,
            UpdatedAt = partner.UpdatedAt
        };
    }
}

