using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[AllowAnonymous]
public class RequestsController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<RequestsController> _logger;
    private readonly ExternalLinksService _externalLinksService;

    public RequestsController(
        ApiClient apiClient, 
        ILogger<RequestsController> logger,
        ExternalLinksService externalLinksService)
    {
        _apiClient = apiClient;
        _logger = logger;
        _externalLinksService = externalLinksService;
    }

    private async Task SetExternalLinksInViewBagAsync()
    {
        var links = await _externalLinksService.GetExternalLinksAsync();
        ViewBag.PrivacyPolicyUrl = links.PrivacyPolicyUrl;
        ViewBag.TermsUrl = links.TermsUrl;
        ViewBag.DataProcessingUrl = links.DataProcessingUrl;
    }

    [HttpGet]
    [Route("Requests/New")]
    [Route("angebot-anfordern")]
    public async Task<IActionResult> New([FromQuery] string? partnerId)
    {
        // Partner validation will be done via API when creating the contact
        if (!string.IsNullOrEmpty(partnerId))
        {
            ViewBag.PartnerId = partnerId;
        }

        await SetExternalLinksInViewBagAsync();
        return View();
    }

    [HttpPost]
    [Route("Requests/New")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromForm] CreateContactDto dto, [FromQuery] string? partnerId, CancellationToken cancellationToken)
    {
        // PartnerId can come from query string or form (hidden field)
        // Form value takes precedence, then query string
        if (string.IsNullOrEmpty(dto.PartnerId) && !string.IsNullOrEmpty(partnerId))
        {
            dto.PartnerId = partnerId;
        }
        // If both are empty, keep dto.PartnerId as is (null or empty)

        if (!ModelState.IsValid)
        {
            if (!string.IsNullOrEmpty(partnerId))
            {
                ViewBag.PartnerId = partnerId;
            }
            await SetExternalLinksInViewBagAsync();
            return View("New", dto);
        }

        // Validate required acceptances
        if (!dto.PrivacyPolicyAccepted || !dto.TermsAccepted || !dto.DataProcessingAccepted)
        {
            if (!dto.PrivacyPolicyAccepted)
                ModelState.AddModelError(nameof(dto.PrivacyPolicyAccepted), "Die Datenschutzerklärung muss akzeptiert werden.");
            if (!dto.TermsAccepted)
                ModelState.AddModelError(nameof(dto.TermsAccepted), "Die AGB müssen akzeptiert werden.");
            if (!dto.DataProcessingAccepted)
                ModelState.AddModelError(nameof(dto.DataProcessingAccepted), "Die Einwilligung zur Datenverarbeitung muss akzeptiert werden.");
            
            if (!string.IsNullOrEmpty(partnerId))
            {
                ViewBag.PartnerId = partnerId;
            }
            await SetExternalLinksInViewBagAsync();
            return View("New", dto);
        }

        try
        {
            _logger.LogInformation("Creating contact with PartnerId: {PartnerId}, Email: {Email}", 
                dto.PartnerId ?? "null", dto.Email);
            
            var (contact, errorMessage) = await _apiClient.CreateContactWithErrorAsync(dto, cancellationToken);
            if (contact == null)
            {
                ModelState.AddModelError("", errorMessage ?? "Ein Fehler ist bei der Anfrage aufgetreten. Bitte versuchen Sie es erneut.");
                if (!string.IsNullOrEmpty(partnerId))
                {
                    ViewBag.PartnerId = partnerId;
                }
                await SetExternalLinksInViewBagAsync();
                return View("New", dto);
            }

            // Create a QuoteRequest for this contact
            _logger.LogInformation("Creating QuoteRequest for contact {ContactId} with PartnerId: {PartnerId}", 
                contact.Id, contact.PartnerId ?? "null");
            
            var quoteRequestDto = new CreateQuoteRequestDto
            {
                ContactId = contact.Id,
                Requirements = dto.Requirements
            };
            var quoteRequest = await _apiClient.CreateQuoteRequestAsync(quoteRequestDto, cancellationToken);
            
            if (quoteRequest == null)
            {
                _logger.LogError("Failed to create QuoteRequest for contact {ContactId}. API returned null.", contact.Id);
                ModelState.AddModelError("", "Die Anfrage wurde erstellt, aber die Angebotsanfrage konnte nicht gespeichert werden. Bitte kontaktieren Sie den Administrator.");
                if (!string.IsNullOrEmpty(partnerId))
                {
                    ViewBag.PartnerId = partnerId;
                }
                await SetExternalLinksInViewBagAsync();
                return View("New", dto);
            }
            
            _logger.LogInformation("Successfully created QuoteRequest {QuoteRequestId} for contact {ContactId}", 
                quoteRequest.Id, contact.Id);

            TempData["SuccessMessage"] = "Anfrage erfolgreich! Vielen Dank für Ihre Anfrage.";
            return RedirectToAction("Confirmation", new { id = contact.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating request");
            ModelState.AddModelError("", "Ein Fehler ist bei der Anfrage aufgetreten. Bitte versuchen Sie es erneut.");
            if (!string.IsNullOrEmpty(partnerId))
            {
                ViewBag.PartnerId = partnerId;
            }
            await SetExternalLinksInViewBagAsync();
            return View("New", dto);
        }
    }

    [HttpGet("Requests/Confirmation/{id}")]
    public async Task<IActionResult> Confirmation(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _apiClient.GetContactForConfirmationAsync(id, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }
        return View(contact);
    }
}

