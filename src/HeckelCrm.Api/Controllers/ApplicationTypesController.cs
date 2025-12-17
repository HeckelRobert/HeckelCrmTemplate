using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Admin")]
public class ApplicationTypesController : ControllerBase
{
    private readonly IApplicationTypeService _applicationTypeService;
    private readonly ILogger<ApplicationTypesController> _logger;

    public ApplicationTypesController(
        IApplicationTypeService applicationTypeService,
        ILogger<ApplicationTypesController> logger)
    {
        _applicationTypeService = applicationTypeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationTypeDto>>> GetAllApplicationTypes(CancellationToken cancellationToken)
    {
        var applicationTypes = await _applicationTypeService.GetAllApplicationTypesAsync(cancellationToken);
        return Ok(applicationTypes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationTypeDto>> GetApplicationTypeById(Guid id, CancellationToken cancellationToken)
    {
        var applicationType = await _applicationTypeService.GetApplicationTypeByIdAsync(id, cancellationToken);
        if (applicationType == null)
        {
            return NotFound();
        }
        return Ok(applicationType);
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationTypeDto>> CreateApplicationType([FromBody] CreateApplicationTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var applicationType = await _applicationTypeService.CreateApplicationTypeAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetApplicationTypeById), new { id = applicationType.Id }, applicationType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApplicationTypeDto>> UpdateApplicationType(Guid id, [FromBody] CreateApplicationTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var applicationType = await _applicationTypeService.UpdateApplicationTypeAsync(id, dto, cancellationToken);
            return Ok(applicationType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteApplicationType(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _applicationTypeService.DeleteApplicationTypeAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

