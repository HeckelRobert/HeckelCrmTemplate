using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly IPartnerRepository _partnerRepository;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        ILeadService leadService,
        IPartnerRepository partnerRepository,
        ILogger<LeadsController> logger)
    {
        _leadService = leadService;
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")] // Only admins can see all leads
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetAllLeads(CancellationToken cancellationToken)
    {
        // Log user claims for debugging
        _logger.LogInformation("GetAllLeads called. User: {User}, IsAuthenticated: {IsAuthenticated}", 
            User.Identity?.Name, User.Identity?.IsAuthenticated);
        foreach (var claim in User.Claims)
        {
            _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
        }
        
        var leads = await _leadService.GetAllLeadsAsync(cancellationToken);
        return Ok(leads);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Partner")] // Partner or Admin can access
    public async Task<ActionResult<LeadDto>> GetLeadById(Guid id, CancellationToken cancellationToken)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, cancellationToken);
        if (lead == null)
        {
            return NotFound();
        }
        return Ok(lead);
    }

    [HttpGet("partner/{partnerId}")]
    [Authorize(Policy = "Partner")] // Partner or Admin can access
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeadsByPartnerId(string partnerId, CancellationToken cancellationToken)
    {
        var leads = await _leadService.GetLeadsByPartnerIdAsync(partnerId, cancellationToken);
        return Ok(leads);
    }

    [HttpPost]
    [AllowAnonymous] // Allow public access for offer requests
    public async Task<ActionResult<LeadDto>> CreateLead([FromBody] CreateLeadDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await _leadService.CreateLeadAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetLeadById), new { id = lead.Id }, lead);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Admin")] // Only Admin can update leads
    public async Task<ActionResult<LeadDto>> UpdateLead(Guid id, [FromBody] CreateLeadDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await _leadService.UpdateLeadAsync(id, dto, cancellationToken);
            return Ok(lead);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "Partner")] // Partner or Admin can access
    public async Task<ActionResult<LeadDto>> UpdateLeadStatus(Guid id, [FromBody] UpdateLeadStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await _leadService.UpdateLeadStatusAsync(id, dto.Status, cancellationToken);
            return Ok(lead);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Admin")] // Only Admin can delete
    public async Task<ActionResult> DeleteLead(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _leadService.DeleteLeadAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id}/create-lexoffice-contact")]
    [Authorize(Policy = "Admin")] // Only Admin can create Lexoffice contacts
    public async Task<ActionResult<LeadDto>> CreateLexofficeContact(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await _leadService.CreateLexofficeContactAsync(id, cancellationToken);
            if (lead == null)
            {
                return NotFound();
            }
            return Ok(lead);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

