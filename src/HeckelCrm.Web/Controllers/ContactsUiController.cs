using System.Security.Claims;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize]
public class ContactsUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<ContactsUiController> _logger;

    public ContactsUiController(ApiClient apiClient, ILogger<ContactsUiController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? partnerId, CancellationToken cancellationToken)
    {
        IEnumerable<ContactDto>? contacts;
        
        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        if (isAdmin.Succeeded)
        {
            // Admin can see all contacts or filter by partnerId
            if (!string.IsNullOrEmpty(partnerId))
            {
                contacts = await _apiClient.GetContactsByPartnerIdAsync(partnerId, cancellationToken) ?? Enumerable.Empty<ContactDto>();
                ViewBag.PartnerId = partnerId;
            }
            else
            {
                contacts = await _apiClient.GetContactsAsync(cancellationToken) ?? Enumerable.Empty<ContactDto>();
            }
        }
        else
        {
            // Partner can only see their own contacts
            var currentPartnerId = HttpContext.Items["PartnerId"] as string;
            if (string.IsNullOrEmpty(currentPartnerId))
            {
                // Try to get partner from EntraId
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
                return RedirectToAction("Setup", "Partner");
            }
            
            contacts = await _apiClient.GetContactsByPartnerIdAsync(currentPartnerId, cancellationToken) ?? Enumerable.Empty<ContactDto>();
            ViewBag.PartnerId = currentPartnerId;
        }

        ViewBag.IsAdmin = isAdmin.Succeeded;
        return View(contacts);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _apiClient.GetContactByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }

        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        // If not admin, check if contact belongs to user's partner
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
            
            if (contact.PartnerId != currentPartnerId)
            {
                return Forbid();
            }
        }

        ViewBag.IsAdmin = isAdmin.Succeeded;
        return View(contact);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBillingStatus(Guid id, [FromForm] string billingStatus, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new UpdateBillingStatusDto { BillingStatus = billingStatus };
            var success = await _apiClient.UpdateBillingStatusAsync(id, dto, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = "Abrechnungsstatus erfolgreich aktualisiert.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Abrechnungsstatus.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating billing status");
            TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Abrechnungsstatus.";
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        if (!isAdmin.Succeeded)
        {
            return Forbid();
        }

        var contact = await _apiClient.GetContactByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }

        // Convert ContactDto to CreateContactDto for editing
        var dto = new CreateContactDto
        {
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            Phone = contact.Phone,
            PartnerId = contact.PartnerId,
            PrivacyPolicyAccepted = contact.PrivacyPolicyAccepted,
            TermsAccepted = contact.TermsAccepted,
            DataProcessingAccepted = contact.DataProcessingAccepted,
            CompanyName = contact.CompanyName,
            CompanyTaxNumber = contact.CompanyTaxNumber,
            CompanyVatRegistrationId = contact.CompanyVatRegistrationId,
            CompanyAllowTaxFreeInvoices = contact.CompanyAllowTaxFreeInvoices,
            BillingStreet = contact.BillingStreet,
            BillingZip = contact.BillingZip,
            BillingCity = contact.BillingCity,
            BillingCountryCode = contact.BillingCountryCode,
            BillingSupplement = contact.BillingSupplement,
            ShippingStreet = contact.ShippingStreet,
            ShippingZip = contact.ShippingZip,
            ShippingCity = contact.ShippingCity,
            ShippingCountryCode = contact.ShippingCountryCode,
            ShippingSupplement = contact.ShippingSupplement,
            EmailBusiness = contact.EmailBusiness,
            EmailOffice = contact.EmailOffice,
            EmailPrivate = contact.EmailPrivate,
            EmailOther = contact.EmailOther,
            PhoneBusiness = contact.PhoneBusiness,
            PhoneOffice = contact.PhoneOffice,
            PhoneMobile = contact.PhoneMobile,
            PhonePrivate = contact.PhonePrivate,
            PhoneFax = contact.PhoneFax,
            PhoneOther = contact.PhoneOther,
            Requirements = contact.Notes,
            Salutation = contact.Salutation
        };

        ViewBag.ContactId = id;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CreateContactDto dto, CancellationToken cancellationToken)
    {
        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        if (!isAdmin.Succeeded)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ContactId = id;
            return View(dto);
        }

        try
        {
            var updatedContact = await _apiClient.UpdateContactAsync(id, dto, cancellationToken);
            if (updatedContact != null)
            {
                TempData["SuccessMessage"] = "Kontakt erfolgreich aktualisiert.";
                return RedirectToAction("Details", new { id });
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Kontakts.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact");
            TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Kontakts.";
        }

        ViewBag.ContactId = id;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        if (!isAdmin.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var success = await _apiClient.DeleteContactAsync(id, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = "Kontakt erfolgreich gelöscht.";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Löschen des Kontakts.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact");
            TempData["ErrorMessage"] = "Fehler beim Löschen des Kontakts.";
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLexofficeContact(Guid id, CancellationToken cancellationToken)
    {
        // Check if user is Admin
        var isAdmin = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, "Admin");
        
        if (!isAdmin.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var contact = await _apiClient.CreateLexofficeContactForContactAsync(id, cancellationToken);
            if (contact != null)
            {
                TempData["SuccessMessage"] = "Kontakt erfolgreich in Lexoffice angelegt.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Anlegen des Kontakts in Lexoffice.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Lexoffice contact for contact {ContactId}", id);
            TempData["ErrorMessage"] = $"Fehler beim Anlegen des Kontakts in Lexoffice: {ex.Message}";
        }

        return RedirectToAction("Details", new { id });
    }
}

