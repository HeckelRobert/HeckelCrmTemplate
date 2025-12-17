using System.Security.Claims;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace HeckelCrm.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApiClient apiClient, IAuthorizationService authorizationService, ILogger<HomeController> logger)
    {
        _apiClient = apiClient;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var isAdmin = await _authorizationService.AuthorizeAsync(User, "Admin");
            _logger.LogInformation("Admin authorization check: Succeeded={Succeeded}, User={User}", isAdmin.Succeeded, User.Identity?.Name);

            // Determine current partner id (for all users, including admins if they have a partner)
            var currentPartnerId = HttpContext.Items["PartnerId"] as string;
            if (string.IsNullOrEmpty(currentPartnerId))
            {
                var entraIdObjectId = User.FindFirstValue("oid") ??
                                       User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(entraIdObjectId))
                {
                    try
                    {
                        var partner = await _apiClient.GetPartnerByEntraIdAsync(entraIdObjectId, cancellationToken);
                        currentPartnerId = partner?.PartnerId ?? string.Empty;
                    }
                    catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
                    {
                        // Partner not found is normal for admins or users without a partner entry
                        _logger.LogDebug("Partner not found for Entra ID {EntraId}. This is normal for admins or users without a partner entry.", entraIdObjectId);
                        currentPartnerId = string.Empty;
                    }
                }
            }
            ViewBag.PartnerId = currentPartnerId;

            IEnumerable<ContactDto> contacts;
            IEnumerable<OfferDto> offers;
            IEnumerable<QuoteRequestDto> quoteRequests;

            if (isAdmin.Succeeded)
            {
                _logger.LogInformation("User is Admin. Retrieving all contacts, offers, and quote requests.");
                contacts = await _apiClient.GetContactsAsync(cancellationToken) ?? Enumerable.Empty<ContactDto>();
                offers = await _apiClient.GetOffersAsync(cancellationToken) ?? Enumerable.Empty<OfferDto>();
                quoteRequests = await _apiClient.GetQuoteRequestsAsync(cancellationToken) ?? Enumerable.Empty<QuoteRequestDto>();
                _logger.LogInformation("Retrieved {ContactCount} contacts, {OfferCount} offers, and {QuoteRequestCount} quote requests for admin", 
                    contacts.Count(), offers.Count(), quoteRequests.Count());
                
                // Log some details about the offers for debugging
                if (offers.Any())
                {
                    _logger.LogInformation("Sample offer statuses: {Statuses}", 
                        string.Join(", ", offers.Take(5).Select(o => $"Id={o.Id}, Status={o.Status}, LexofficeStatus={o.LexofficeVoucherStatus}")));
                }
            }
            else
            {
                if (string.IsNullOrEmpty(currentPartnerId))
                {
                    contacts = Enumerable.Empty<ContactDto>();
                    offers = Enumerable.Empty<OfferDto>();
                    quoteRequests = Enumerable.Empty<QuoteRequestDto>();
                }
                else
                {
                    contacts = await _apiClient.GetContactsByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<ContactDto>();
                    offers = await _apiClient.GetOffersByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<OfferDto>();
                    
                    // For partners, get quote requests through their contacts
                    var allQuoteRequests = new List<QuoteRequestDto>();
                    foreach (var contact in contacts)
                    {
                        var contactQuoteRequests = await _apiClient.GetQuoteRequestsByContactIdAsync(contact.Id, cancellationToken) ?? Enumerable.Empty<QuoteRequestDto>();
                        allQuoteRequests.AddRange(contactQuoteRequests);
                    }
                    quoteRequests = allQuoteRequests;
                }
            }

            ViewBag.ContactsCount = contacts.Count();
            ViewBag.OffersCount = offers.Count();
            ViewBag.QuoteRequestsCount = quoteRequests.Count();

            _logger.LogInformation("Calculating accepted and pending offers from {OfferCount} total offers", offers.Count());

            // Accepted offers: Lexoffice status is "accepted" or "archived", OR internal status is "InProgress"
            var acceptedCount = offers.Count(a =>
                string.Equals(a.LexofficeVoucherStatus, "accepted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.LexofficeVoucherStatus, "archived", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Status, "InProgress", StringComparison.OrdinalIgnoreCase));

            // Pending offers: Exclude accepted, archived, declined, and rejected offers
            // Include: open, sent, viewed, draft, or no Lexoffice status (but not rejected)
            var pendingCount = offers.Count(a =>
                !string.Equals(a.LexofficeVoucherStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.LexofficeVoucherStatus, "archived", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.LexofficeVoucherStatus, "declined", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Status, "InProgress", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Status, "Rejected", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(a.LexofficeVoucherStatus) ||
                 string.Equals(a.LexofficeVoucherStatus, "open", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(a.LexofficeVoucherStatus, "sent", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(a.LexofficeVoucherStatus, "viewed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(a.LexofficeVoucherStatus, "draft", StringComparison.OrdinalIgnoreCase)));

            _logger.LogInformation("Calculated statistics: {AcceptedCount} accepted, {PendingCount} pending offers", acceptedCount, pendingCount);

            ViewBag.AcceptedOffersCount = acceptedCount;
            ViewBag.PendingOffersCount = pendingCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard statistics");
            ViewBag.ContactsCount = 0;
            ViewBag.OffersCount = 0;
            ViewBag.QuoteRequestsCount = 0;
            ViewBag.AcceptedOffersCount = 0;
            ViewBag.PendingOffersCount = 0;
        }

        return View();
    }
    
    [AllowAnonymous]
    public IActionResult Welcome()
    {
        // If user is already authenticated, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index");
        }
        return View();
    }
}

