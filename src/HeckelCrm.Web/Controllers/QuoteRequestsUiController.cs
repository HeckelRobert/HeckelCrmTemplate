using System.Security.Claims;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize]
public class QuoteRequestsUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<QuoteRequestsUiController> _logger;

    public QuoteRequestsUiController(ApiClient apiClient, ILogger<QuoteRequestsUiController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(Guid? contactId, string? status, CancellationToken cancellationToken)
    {
        IEnumerable<QuoteRequestDto>? quoteRequests;
        bool isAdminSucceeded = false;
        
        try
        {
            // Check if user is Admin
            var isAdmin = await HttpContext.RequestServices
                .GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(User, "Admin");
            isAdminSucceeded = isAdmin.Succeeded;
            
            if (isAdmin.Succeeded)
            {
                // Admin can see all quote requests or filter by contactId
                _logger.LogInformation("User is Admin. Retrieving quote requests.");
                if (contactId.HasValue)
                {
                    _logger.LogInformation("Filtering quote requests by contactId: {ContactId}", contactId.Value);
                    quoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(contactId.Value, cancellationToken) ?? Enumerable.Empty<QuoteRequestDto>();
                    _logger.LogInformation("Retrieved {Count} quote requests for contact {ContactId}", quoteRequests.Count(), contactId.Value);
                    ViewBag.ContactId = contactId.Value;
                }
                else
                {
                    _logger.LogInformation("Retrieving all quote requests for admin");
                    quoteRequests = await _apiClient.GetQuoteRequestsAsync(cancellationToken) ?? Enumerable.Empty<QuoteRequestDto>();
                    _logger.LogInformation("Retrieved {Count} quote requests for admin", quoteRequests.Count());
                }
            }
        else
        {
            // Partner can only see quote requests for their contacts
            var currentPartnerId = HttpContext.Items["PartnerId"] as string;
            if (string.IsNullOrEmpty(currentPartnerId))
            {
                var entraIdObjectId = User.FindFirstValue("oid") ?? 
                                     User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(entraIdObjectId))
                {
                    var partner = await _apiClient.GetPartnerByEntraIdAsync(entraIdObjectId, cancellationToken);
                    currentPartnerId = partner?.PartnerId;
                }
            }
            
            if (string.IsNullOrEmpty(currentPartnerId))
            {
                _logger.LogWarning("Partner ID not found for user. Redirecting to Partner setup.");
                return RedirectToAction("Setup", "Partner");
            }

            // Get all contacts for this partner, then get quote requests
            var contacts = await _apiClient.GetContactsByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<ContactDto>();
            _logger.LogInformation("Found {ContactCount} contacts for partner {PartnerId}", contacts.Count(), currentPartnerId);
            
            var allQuoteRequests = new List<QuoteRequestDto>();
            
            foreach (var contact in contacts)
            {
                var contactQuoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(contact.Id, cancellationToken) ?? Enumerable.Empty<QuoteRequestDto>();
                _logger.LogInformation("Found {QuoteRequestCount} quote requests for contact {ContactId} ({ContactEmail})", 
                    contactQuoteRequests.Count(), contact.Id, contact.Email);
                allQuoteRequests.AddRange(contactQuoteRequests);
            }
            
            _logger.LogInformation("Total quote requests found for partner {PartnerId}: {TotalCount}", currentPartnerId, allQuoteRequests.Count);
            quoteRequests = allQuoteRequests;
            ViewBag.PartnerId = currentPartnerId;
        }

            // Apply status filter
            if (!string.IsNullOrEmpty(status))
            {
                quoteRequests = quoteRequests.Where(qr => 
                    !string.IsNullOrEmpty(qr.Status) && 
                    qr.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                ViewBag.Status = status;
            }

            ViewBag.IsAdmin = isAdminSucceeded;
            return View(quoteRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quote requests");
            TempData["ErrorMessage"] = "Fehler beim Laden der Anfragen. Bitte versuchen Sie es erneut.";
            return View(Enumerable.Empty<QuoteRequestDto>());
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var quoteRequest = await _apiClient.GetQuoteRequestByIdAsync(id, cancellationToken);
        if (quoteRequest == null)
        {
            return NotFound();
        }

        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        // If not admin, check if quote request belongs to user's partner
        if (!isAdmin.Succeeded)
        {
            var currentPartnerId = HttpContext.Items["PartnerId"] as string;
            if (string.IsNullOrEmpty(currentPartnerId))
            {
                var entraIdObjectId = User.FindFirstValue("oid") ?? 
                                     User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(entraIdObjectId))
                {
                    var partner = await _apiClient.GetPartnerByEntraIdAsync(entraIdObjectId, cancellationToken);
                    currentPartnerId = partner?.PartnerId;
                }
            }
            
            // Get contact to check partner
            var contact = await _apiClient.GetContactByIdAsync(quoteRequest.ContactId, cancellationToken);
            if (contact?.PartnerId != currentPartnerId)
            {
                return Forbid();
            }
        }

        // Load angebote for this quote request
        var angebote = await _apiClient.GetOffersByQuoteRequestIdAsync(id, cancellationToken) ?? Enumerable.Empty<OfferDto>();
        ViewBag.Angebote = angebote;
        ViewBag.IsAdmin = isAdmin.Succeeded;
        
        return View(quoteRequest);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromForm] string status, [FromForm] Guid? selectedQuoteId, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new UpdateRequestStatusDto 
            { 
                Status = status,
                SelectedQuoteId = selectedQuoteId
            };
            var success = await _apiClient.UpdateRequestStatusAsync(id, dto, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = "Status erfolgreich aktualisiert.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Status.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quote request status");
            TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Status.";
        }

        return RedirectToAction("Details", new { id });
    }
}

