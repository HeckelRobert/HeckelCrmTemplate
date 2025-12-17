using System.Security.Claims;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize]
public class PartnerController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<PartnerController> _logger;

    public PartnerController(ApiClient apiClient, ILogger<PartnerController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet]
    [Route("Partner/Setup")]
    public IActionResult Setup()
    {
        var entraIdObjectId = User.FindFirstValue("oid") ?? 
                              User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name") ?? 
                   User.FindFirstValue(ClaimTypes.Name) ?? 
                   User.Identity?.Name ?? "";
        var email = User.FindFirstValue("preferred_username") ?? 
                   User.FindFirstValue(ClaimTypes.Email) ?? "";

        ViewBag.EntraIdObjectId = entraIdObjectId;
        ViewBag.Name = name;
        ViewBag.Email = email;

        return View();
    }

    [HttpPost]
    [Route("Partner/Setup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup([FromForm] string partnerId, CancellationToken cancellationToken)
    {
        var entraIdObjectId = User.FindFirstValue("oid") ?? 
                              User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name") ?? 
                   User.FindFirstValue(ClaimTypes.Name) ?? 
                   User.Identity?.Name ?? "";
        var email = User.FindFirstValue("preferred_username") ?? 
                   User.FindFirstValue(ClaimTypes.Email) ?? "";

        if (string.IsNullOrWhiteSpace(partnerId))
        {
            ModelState.AddModelError("partnerId", "Partner-ID ist erforderlich.");
            ViewBag.EntraIdObjectId = entraIdObjectId;
            ViewBag.Name = name;
            ViewBag.Email = email;
            return View();
        }

        if (string.IsNullOrEmpty(entraIdObjectId))
        {
            ModelState.AddModelError("", "Entra ID Object ID nicht gefunden.");
            ViewBag.EntraIdObjectId = entraIdObjectId;
            ViewBag.Name = name;
            ViewBag.Email = email;
            return View();
        }

        try
        {
            var createDto = new CreatePartnerDto
            {
                PartnerId = partnerId.Trim(),
                Name = name,
                Email = email,
                EntraIdObjectId = entraIdObjectId
            };

            var partner = await _apiClient.CreateOrGetPartnerAsync(createDto, cancellationToken);
            
            if (partner == null)
            {
                _logger.LogError("CreateOrGetPartnerAsync returned null for EntraId: {EntraId}", entraIdObjectId);
                ModelState.AddModelError("", "Fehler beim Erstellen des Partners. Die API hat keine Antwort zurückgegeben.");
                ViewBag.EntraIdObjectId = entraIdObjectId;
                ViewBag.Name = name;
                ViewBag.Email = email;
                return View();
            }
            
            TempData["SuccessMessage"] = $"Partner erfolgreich erstellt! Ihre Partner-ID ist: {partner.PartnerId}";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating partner");
            ModelState.AddModelError("", $"Fehler beim Erstellen des Partners: {ex.Message}");
            ViewBag.EntraIdObjectId = entraIdObjectId;
            ViewBag.Name = name;
            ViewBag.Email = email;
            return View();
        }
    }
}

