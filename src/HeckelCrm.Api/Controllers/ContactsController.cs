using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly ILogger<ContactsController> _logger;

    public ContactsController(IContactService contactService, ILogger<ContactsController> logger)
    {
        _contactService = contactService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")] // Only admins can see all contacts
    public async Task<ActionResult<IEnumerable<ContactDto>>> GetAllContacts(CancellationToken cancellationToken)
    {
        var contacts = await _contactService.GetAllContactsAsync(cancellationToken);
        return Ok(contacts);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Partner")] // Partners and admins can view contacts
    public async Task<ActionResult<ContactDto>> GetContactById(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _contactService.GetContactByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }
        return Ok(contact);
    }

    [HttpGet("partner/{partnerId}")]
    [Authorize(Policy = "Partner")] // Partners can view their contacts
    public async Task<ActionResult<IEnumerable<ContactDto>>> GetContactsByPartnerId(string partnerId, CancellationToken cancellationToken)
    {
        var contacts = await _contactService.GetContactsByPartnerIdAsync(partnerId, cancellationToken);
        return Ok(contacts);
    }

    [HttpPost]
    [AllowAnonymous] // Allow anonymous contact creation for quote requests
    public async Task<ActionResult<ContactDto>> CreateContact([FromBody] CreateContactDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var contact = await _contactService.CreateContactAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetContactById), new { id = contact.Id }, contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Admin")] // Only admins can update contacts
    public async Task<ActionResult<ContactDto>> UpdateContact(Guid id, [FromBody] CreateContactDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var contact = await _contactService.UpdateContactAsync(id, dto, cancellationToken);
            return Ok(contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/billing-status")]
    [Authorize(Policy = "Partner")] // Partners and admins can update billing status
    public async Task<ActionResult<ContactDto>> UpdateBillingStatus(Guid id, [FromBody] UpdateBillingStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // Check if user is admin
            var isAdmin = User.HasClaim("groups", HttpContext.RequestServices.GetRequiredService<IConfiguration>()["AzureAd:AdminGroupId"] ?? "") ||
                         User.IsInRole("Admin");

            var contact = await _contactService.UpdateBillingStatusAsync(id, dto, isAdmin, cancellationToken);
            return Ok(contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Admin")] // Only admins can delete contacts
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _contactService.DeleteContactAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id}/lexoffice-contact")]
    [Authorize(Policy = "Admin")] // Only admins can create Lexoffice contacts
    public async Task<ActionResult<ContactDto>> CreateLexofficeContact(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var contact = await _contactService.CreateLexofficeContactAsync(id, cancellationToken);
            if (contact == null)
            {
                return NotFound();
            }
            return Ok(contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/confirmation")]
    [AllowAnonymous] // Allow anonymous access for confirmation page
    public async Task<ActionResult<ContactDto>> GetContactForConfirmation(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _contactService.GetContactByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }
        return Ok(contact);
    }
}

