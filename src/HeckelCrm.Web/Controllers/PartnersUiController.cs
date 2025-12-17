using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize(Policy = "Admin")]
public class PartnersUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<PartnersUiController> _logger;

    public PartnersUiController(ApiClient apiClient, ILogger<PartnersUiController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var partners = await _apiClient.GetAllPartnersAsync(cancellationToken) ?? Enumerable.Empty<Core.DTOs.PartnerDto>();
            return View(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading partners");
            ViewBag.ErrorMessage = "Fehler beim Laden der Partner.";
            return View(Enumerable.Empty<Core.DTOs.PartnerDto>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string partnerId, [FromForm] string newPartnerId, [FromForm] string name, [FromForm] string email, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new Core.DTOs.UpdatePartnerDto
            {
                PartnerId = newPartnerId,
                Name = name,
                Email = email
            };

            var partner = await _apiClient.UpdatePartnerAsync(partnerId, dto, cancellationToken);
            if (partner != null)
            {
                TempData["SuccessMessage"] = "Partner erfolgreich aktualisiert.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Partners.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating partner");
            TempData["ErrorMessage"] = $"Fehler beim Aktualisieren des Partners: {ex.Message}";
        }

        return RedirectToAction("Index");
    }
}

