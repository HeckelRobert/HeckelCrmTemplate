using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize(Policy = "Admin")]
public class AdminSettingsUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<AdminSettingsUiController> _logger;

    public AdminSettingsUiController(ApiClient apiClient, ILogger<AdminSettingsUiController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _apiClient.GetAdminSettingsAsync(cancellationToken);
        if (settings == null)
        {
            // Initialize with empty values if not set
            settings = new AdminSettingsDto();
        }

        var viewModel = new UpdateAdminSettingsDto
        {
            DefaultUnitPrice = settings.DefaultUnitPrice ?? 0,
            DefaultTaxRatePercentage = settings.DefaultTaxRatePercentage ?? 0,
            DefaultValidUntilDays = settings.DefaultValidUntilDays ?? 0,
            // Show placeholder value if key exists, but don't expose the actual key
            LexofficeApiKey = !string.IsNullOrEmpty(settings.LexofficeApiKey) ? "***EXISTS***" : string.Empty,
            PrivacyPolicyUrl = settings.PrivacyPolicyUrl ?? string.Empty,
            TermsUrl = settings.TermsUrl ?? string.Empty,
            DataProcessingUrl = settings.DataProcessingUrl ?? string.Empty,
            ImprintUrl = settings.ImprintUrl ?? string.Empty
        };
        
        // Store actual key in ViewBag for display purposes (to show if key is set)
        ViewBag.LexofficeApiKeyExists = !string.IsNullOrEmpty(settings.LexofficeApiKey);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(UpdateAdminSettingsDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        try
        {
            // If LexofficeApiKey is empty or placeholder, preserve the existing value
            if (string.IsNullOrWhiteSpace(dto.LexofficeApiKey) || dto.LexofficeApiKey == "***EXISTS***")
            {
                var currentSettings = await _apiClient.GetAdminSettingsAsync(cancellationToken);
                if (currentSettings != null && !string.IsNullOrEmpty(currentSettings.LexofficeApiKey))
                {
                    dto.LexofficeApiKey = currentSettings.LexofficeApiKey;
                }
                else
                {
                    dto.LexofficeApiKey = null;
                }
            }

            var settings = await _apiClient.UpdateAdminSettingsAsync(dto, cancellationToken);
            if (settings != null)
            {
                TempData["SuccessMessage"] = "Admin-Einstellungen erfolgreich gespeichert.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Speichern der Admin-Einstellungen.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin settings");
            TempData["ErrorMessage"] = $"Fehler beim Speichern: {ex.Message}";
        }

        return View(dto);
    }
}

