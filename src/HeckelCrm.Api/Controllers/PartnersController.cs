using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerService _partnerService;
    private readonly ILogger<PartnersController> _logger;

    public PartnersController(
        IPartnerService partnerService,
        ILogger<PartnersController> logger)
    {
        _partnerService = partnerService;
        _logger = logger;
    }

    [HttpPost("create-or-get")]
    [Authorize]
    public async Task<ActionResult<PartnerDto>> CreateOrGetPartner([FromBody] CreatePartnerDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var partner = await _partnerService.CreateOrGetPartnerAsync(dto, cancellationToken);
            return Ok(partner);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("by-entra-id/{entraIdObjectId}")]
    [Authorize]
    public async Task<ActionResult<PartnerDto>> GetPartnerByEntraId(string entraIdObjectId, CancellationToken cancellationToken)
    {
        var partner = await _partnerService.GetPartnerByEntraIdAsync(entraIdObjectId, cancellationToken);
        if (partner == null)
        {
            return NotFound();
        }
        return Ok(partner);
    }

    [HttpGet("by-partner-id/{partnerId}")]
    [Authorize]
    public async Task<ActionResult<PartnerDto>> GetPartnerByPartnerId(string partnerId, CancellationToken cancellationToken)
    {
        var partner = await _partnerService.GetPartnerByPartnerIdAsync(partnerId, cancellationToken);
        if (partner == null)
        {
            return NotFound();
        }
        return Ok(partner);
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<IEnumerable<PartnerDto>>> GetAllPartners(CancellationToken cancellationToken)
    {
        var partners = await _partnerService.GetAllPartnersAsync(cancellationToken);
        return Ok(partners);
    }

    [HttpPut("{partnerId}")]
    [Authorize]
    public async Task<ActionResult<PartnerDto>> UpdatePartner(string partnerId, [FromBody] UpdatePartnerDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var partner = await _partnerService.UpdatePartnerAsync(partnerId, dto, cancellationToken);
            return Ok(partner);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

