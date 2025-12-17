using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuoteRequestsController : ControllerBase
{
    private readonly IQuoteRequestService _quoteRequestService;
    private readonly ILogger<QuoteRequestsController> _logger;

    public QuoteRequestsController(IQuoteRequestService quoteRequestService, ILogger<QuoteRequestsController> logger)
    {
        _quoteRequestService = quoteRequestService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")] // Only admins can see all quote requests
    public async Task<ActionResult<IEnumerable<QuoteRequestDto>>> GetAllQuoteRequests(CancellationToken cancellationToken)
    {
        var quoteRequests = await _quoteRequestService.GetAllQuoteRequestsAsync(cancellationToken);
        return Ok(quoteRequests);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Partner")] // Partners and admins can view quote requests
    public async Task<ActionResult<QuoteRequestDto>> GetQuoteRequestById(Guid id, CancellationToken cancellationToken)
    {
        var quoteRequest = await _quoteRequestService.GetQuoteRequestByIdAsync(id, cancellationToken);
        if (quoteRequest == null)
        {
            return NotFound();
        }
        return Ok(quoteRequest);
    }

    [HttpGet("contact/{contactId}")]
    [Authorize(Policy = "Partner")] // Partners and admins can view quote requests by contact
    public async Task<ActionResult<IEnumerable<QuoteRequestDto>>> GetQuoteRequestsByContactId(Guid contactId, CancellationToken cancellationToken)
    {
        var quoteRequests = await _quoteRequestService.GetQuoteRequestsByContactIdAsync(contactId, cancellationToken);
        return Ok(quoteRequests);
    }

    [HttpPost]
    [AllowAnonymous] // Allow anonymous quote request creation
    public async Task<ActionResult<QuoteRequestDto>> CreateQuoteRequest([FromBody] CreateQuoteRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var quoteRequest = await _quoteRequestService.CreateQuoteRequestAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetQuoteRequestById), new { id = quoteRequest.Id }, quoteRequest);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "Admin")] // Only admins can update request status
    public async Task<ActionResult<QuoteRequestDto>> UpdateRequestStatus(Guid id, [FromBody] UpdateRequestStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var quoteRequest = await _quoteRequestService.UpdateRequestStatusAsync(id, dto, cancellationToken);
            return Ok(quoteRequest);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Admin")] // Only admins can delete quote requests
    public async Task<IActionResult> DeleteQuoteRequest(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _quoteRequestService.DeleteQuoteRequestAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}

