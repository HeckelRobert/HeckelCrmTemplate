using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using static HeckelCrm.Core.Interfaces.ILexofficeService;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HeckelCrm.Web.Controllers;

[Authorize]
public class OffersUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<OffersUiController> _logger;
    private readonly IAuthorizationService _authorizationService;

    public OffersUiController(
        ApiClient apiClient, 
        ILogger<OffersUiController> logger,
        IAuthorizationService authorizationService)
    {
        _apiClient = apiClient;
        _logger = logger;
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index(
        string? partnerId, 
        Guid? contactId, 
        Guid? quoteRequestId, 
        string? lexofficeStatus,
        string? billingStatus,
        CancellationToken cancellationToken)
    {
        var isAdminResult = await _authorizationService.AuthorizeAsync(User, "Admin");
        var isAdmin = isAdminResult.Succeeded;

        IEnumerable<OfferDto>? offers;

        if (quoteRequestId.HasValue)
        {
            offers = await _apiClient.GetOffersByQuoteRequestIdAsync(quoteRequestId.Value, cancellationToken) ?? Enumerable.Empty<OfferDto>();
            ViewBag.QuoteRequestId = quoteRequestId.Value;
        }
        else if (contactId.HasValue)
        {
            offers = await _apiClient.GetOffersByContactIdAsync(contactId.Value, cancellationToken) ?? Enumerable.Empty<OfferDto>();
            ViewBag.ContactId = contactId.Value;
        }
        else if (!string.IsNullOrEmpty(partnerId))
        {
            offers = await _apiClient.GetOffersByPartnerIdAsync(partnerId, cancellationToken) ?? Enumerable.Empty<OfferDto>();
            ViewBag.PartnerId = partnerId;
        }
        else if (!isAdmin)
        {
            // Partner should automatically see their own offers
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
                offers = Enumerable.Empty<OfferDto>();
            }
            else
            {
                offers = await _apiClient.GetOffersByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<OfferDto>();
                ViewBag.PartnerId = currentPartnerId;
            }
        }
        else
        {
            // Admin sees all offers
            offers = await _apiClient.GetOffersAsync(cancellationToken) ?? Enumerable.Empty<OfferDto>();
        }

        // Apply filters
        if (!string.IsNullOrEmpty(lexofficeStatus))
        {
            offers = offers.Where(a => 
                !string.IsNullOrEmpty(a.LexofficeVoucherStatus) && 
                a.LexofficeVoucherStatus.Equals(lexofficeStatus, StringComparison.OrdinalIgnoreCase));
            ViewBag.LexofficeStatus = lexofficeStatus;
        }

        if (!string.IsNullOrEmpty(billingStatus))
        {
            offers = offers.Where(a => 
                !string.IsNullOrEmpty(a.BillingStatus) && 
                a.BillingStatus.Equals(billingStatus, StringComparison.OrdinalIgnoreCase));
            ViewBag.BillingStatus = billingStatus;
        }

        ViewBag.IsAdmin = isAdmin;
        return View(offers);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _apiClient.GetOfferByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            return NotFound();
        }

        // Load the associated quote request
        var quoteRequest = await _apiClient.GetQuoteRequestByIdAsync(offer.QuoteRequestId, cancellationToken);
        ViewBag.QuoteRequest = quoteRequest;

        return View(offer);
    }

    [HttpPost]
    public async Task<IActionResult> SyncWithLexoffice(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _apiClient.SyncOfferWithLexofficeAsync(id, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = "Angebot erfolgreich mit Lexoffice synchronisiert.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Synchronisieren mit Lexoffice.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Offer {OfferId}", id);
            TempData["ErrorMessage"] = $"Fehler beim Synchronisieren: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> LoadFromLexoffice(Guid contactId, CancellationToken cancellationToken)
    {
        try
        {
            var offers = await _apiClient.LoadOffersFromLexofficeAsync(contactId, cancellationToken);
            if (offers != null && offers.Any())
            {
                TempData["SuccessMessage"] = $"{offers.Count()} Offer(e) erfolgreich von Lexoffice geladen.";
            }
            else
            {
                TempData["InfoMessage"] = "Keine neuen offers von Lexoffice gefunden.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load offers from Lexoffice for contact {ContactId}", contactId);
            TempData["ErrorMessage"] = $"Fehler beim Laden der offers: {ex.Message}";
        }

        return RedirectToAction("Details", "ContactsUi", new { id = contactId });
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create(Guid? contactId, CancellationToken cancellationToken)
    {
        var viewModel = new CreateOfferViewModel
        {
            ContactId = contactId
        };

        if (contactId.HasValue)
        {
            var contact = await _apiClient.GetContactByIdAsync(contactId.Value, cancellationToken);
            if (contact == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(contact.LexofficeContactId))
            {
                TempData["ErrorMessage"] = "Der Kontakt muss zuerst in Lexoffice angelegt werden, bevor ein Offer erstellt werden kann.";
                return RedirectToAction("Details", "ContactsUi", new { id = contactId.Value });
            }

            viewModel.ContactName = $"{contact.FirstName} {contact.LastName}";
            viewModel.ContactEmail = contact.Email;

            var quoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(contactId.Value, cancellationToken);
            viewModel.QuoteRequests = quoteRequests?.ToList() ?? new List<QuoteRequestDto>();

            var articles = await _apiClient.GetArticlesAsync(cancellationToken);
            viewModel.Articles = articles?.ToList() ?? new List<ArticleInfo>();

            var applicationTypes = await _apiClient.GetApplicationTypesAsync(cancellationToken);
            viewModel.ApplicationTypes = applicationTypes?.ToList() ?? new List<ApplicationTypeDto>();
        }
        else
        {
            var contacts = await _apiClient.GetContactsAsync(cancellationToken);
            viewModel.Contacts = contacts?.Where(c => !string.IsNullOrEmpty(c.LexofficeContactId)).ToList() ?? new List<ContactDto>();

            var articles = await _apiClient.GetArticlesAsync(cancellationToken);
            viewModel.Articles = articles?.ToList() ?? new List<ArticleInfo>();

            var applicationTypes = await _apiClient.GetApplicationTypesAsync(cancellationToken);
            viewModel.ApplicationTypes = applicationTypes?.ToList() ?? new List<ApplicationTypeDto>();
        }

        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOfferViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (viewModel.ContactId.HasValue)
            {
                var contact = await _apiClient.GetContactByIdAsync(viewModel.ContactId.Value, cancellationToken);
                if (contact != null)
                {
                    viewModel.ContactName = $"{contact.FirstName} {contact.LastName}";
                    viewModel.ContactEmail = contact.Email;
                }

                var quoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(viewModel.ContactId.Value, cancellationToken);
                viewModel.QuoteRequests = quoteRequests?.ToList() ?? new List<QuoteRequestDto>();
            }

            var articles = await _apiClient.GetArticlesAsync(cancellationToken);
            viewModel.Articles = articles?.ToList() ?? new List<ArticleInfo>();

            var applicationTypes = await _apiClient.GetApplicationTypesAsync(cancellationToken);
            viewModel.ApplicationTypes = applicationTypes?.ToList() ?? new List<ApplicationTypeDto>();

            return View(viewModel);
        }

        try
        {
            // Get admin settings from database
            var adminSettings = await _apiClient.GetAdminSettingsAsync(cancellationToken);
            if (adminSettings == null || !adminSettings.DefaultUnitPrice.HasValue || !adminSettings.DefaultTaxRatePercentage.HasValue || !adminSettings.DefaultValidUntilDays.HasValue)
            {
                TempData["ErrorMessage"] = "Bitte konfigurieren Sie zuerst die Admin-Einstellungen (Tagessatz, MwSt., Gültigkeitsdauer) bevor Sie ein Angebot erstellen.";
                return RedirectToAction("Index", "AdminSettingsUi");
            }

            var dto = new CreateOfferDto
            {
                QuoteRequestIds = viewModel.SelectedQuoteRequestIds ?? new List<Guid>(),
                Title = viewModel.Title, // Will be generated in service from ApplicationType and first line item
                Description = string.Empty,
                Currency = viewModel.Currency,
                ValidUntil = viewModel.ValidUntil ?? DateTime.UtcNow.AddDays(adminSettings.DefaultValidUntilDays.Value),
                ApplicationTypeId = viewModel.ApplicationTypeId,
                LineItems = viewModel.LineItems?.Select(item => new OfferLineItemDto
                {
                    ArticleId = item.ArticleId ?? string.Empty,
                    Name = item.Name ?? string.Empty,
                    Description = item.Description ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitName = item.UnitName,
                    UnitPrice = item.UnitPrice ?? adminSettings.DefaultUnitPrice.Value,
                    TaxRatePercentage = item.TaxRatePercentage ?? adminSettings.DefaultTaxRatePercentage.Value,
                    Days = item.Days ?? 1
                }).ToList() ?? new List<OfferLineItemDto>()
            };

            var offer = await _apiClient.CreateOfferAsync(dto, cancellationToken);
            if (offer != null)
            {
                TempData["SuccessMessage"] = "Offer erfolgreich erstellt.";
                return RedirectToAction("Details", new { id = offer.Id });
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Erstellen des Offers.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Offer");
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" ({ex.InnerException.Message})";
            }
            TempData["ErrorMessage"] = $"Fehler beim Erstellen des Offers: {errorMessage}";
        }

        if (viewModel.ContactId.HasValue)
        {
            var contact = await _apiClient.GetContactByIdAsync(viewModel.ContactId.Value, cancellationToken);
            if (contact != null)
            {
                viewModel.ContactName = $"{contact.FirstName} {contact.LastName}";
                viewModel.ContactEmail = contact.Email;
            }

            var quoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(viewModel.ContactId.Value, cancellationToken);
            viewModel.QuoteRequests = quoteRequests?.ToList() ?? new List<QuoteRequestDto>();
        }

        var articlesList2 = await _apiClient.GetArticlesAsync(cancellationToken);
        viewModel.Articles = articlesList2?.ToList() ?? new List<ArticleInfo>();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBillingStatus(Guid id, [FromForm] string billingStatus, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new UpdateBillingStatusDto { BillingStatus = billingStatus };
            var (success, errorMessage) = await _apiClient.UpdateOfferBillingStatusAsync(id, dto, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = "Abrechnungsstatus erfolgreich aktualisiert.";
            }
            else
            {
                TempData["ErrorMessage"] = errorMessage ?? "Fehler beim Aktualisieren des Abrechnungsstatus.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Offer billing status");
            TempData["ErrorMessage"] = $"Fehler beim Aktualisieren des Abrechnungsstatus: {ex.Message}";
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchSyncWithLexoffice(CancellationToken cancellationToken)
    {
        try
        {
            var isAdminResult = await _authorizationService.AuthorizeAsync(User, "Admin");
            var isAdmin = isAdminResult.Succeeded;

            if (isAdmin)
            {
                // Admin syncs all offers with Lexoffice quote ID
                var success = await _apiClient.BatchSyncOffersWithLexofficeAsync(cancellationToken);
                if (success)
                {
                    TempData["SuccessMessage"] = "Alle Angebote mit Lexoffice-ID erfolgreich synchronisiert.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Fehler beim Synchronisieren der Angebote mit Lexoffice.";
                }
            }
            else
            {
                // Partner syncs only their own offers
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
                    TempData["ErrorMessage"] = "Partner-ID nicht gefunden.";
                    return RedirectToAction("Index");
                }

                // Get partner's offers and sync them individually (only those with Lexoffice quote ID)
                var offers = await _apiClient.GetOffersByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<OfferDto>();
                var offersWithLexofficeId = offers.Where(o => !string.IsNullOrEmpty(o.LexofficeQuoteId)).ToList();
                var syncedCount = 0;
                var errorCount = 0;

                foreach (var offer in offersWithLexofficeId)
                {
                    try
                    {
                        var success = await _apiClient.SyncOfferWithLexofficeAsync(offer.Id, cancellationToken);        
                        if (success)
                        {
                            syncedCount++;
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing Offer {OfferId} with Lexoffice", offer.Id);
                        errorCount++;
                    }
                }

                if (syncedCount > 0)
                {
                    TempData["SuccessMessage"] = $"{syncedCount} Offer(e) erfolgreich synchronisiert.";
                }
                if (errorCount > 0)
                {
                    TempData["ErrorMessage"] = $"Fehler beim Synchronisieren von {errorCount} Offer(en).";
                }
                if (syncedCount == 0 && errorCount == 0)
                {
                    TempData["InfoMessage"] = "Keine Angebote mit Lexoffice-ID zum Synchronisieren gefunden.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch syncing offers with Lexoffice");
            TempData["ErrorMessage"] = $"Fehler beim Synchronisieren: {ex.Message}";
        }

        return RedirectToAction("Index");
    }
}

public class CreateOfferViewModel
{
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public List<Guid>? SelectedQuoteRequestIds { get; set; }
    public string? Title { get; set; } // Auto-generated, not required from user
    public string? Description { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime? ValidUntil { get; set; }
    public Guid? ApplicationTypeId { get; set; }
    public List<QuoteRequestDto> QuoteRequests { get; set; } = new();
    public List<ContactDto> Contacts { get; set; } = new();
    public List<ArticleInfo> Articles { get; set; } = new();
    public List<ApplicationTypeDto> ApplicationTypes { get; set; } = new();
    public List<OfferLineItemViewModel>? LineItems { get; set; }
}

public class OfferLineItemViewModel
{
    public string? ArticleId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string UnitName { get; set; } = "Tage";
    public decimal? UnitPrice { get; set; }
    public int? TaxRatePercentage { get; set; }
    public int? Days { get; set; }
}

