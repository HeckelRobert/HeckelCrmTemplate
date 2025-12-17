using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminSettingsController : ControllerBase
{
    private readonly IAdminSettingsService _adminSettingsService;
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(
        IAdminSettingsService adminSettingsService,
        ILogger<AdminSettingsController> logger)
    {
        _adminSettingsService = adminSettingsService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous] // Allow anonymous access for external links (used in footer)
    public async Task<ActionResult<AdminSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _adminSettingsService.GetSettingsAsync(cancellationToken);
        // If not authenticated, only return external links (for public access)
        if (!User.Identity?.IsAuthenticated == true)
        {
            return Ok(new AdminSettingsDto
            {
                PrivacyPolicyUrl = settings.PrivacyPolicyUrl,
                TermsUrl = settings.TermsUrl,
                DataProcessingUrl = settings.DataProcessingUrl,
                ImprintUrl = settings.ImprintUrl
            });
        }
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<AdminSettingsDto>> UpdateSettings([FromBody] UpdateAdminSettingsDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _adminSettingsService.UpdateSettingsAsync(dto, cancellationToken);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin settings");
            return BadRequest($"Fehler beim Aktualisieren der Einstellungen: {ex.Message}");
        }
    }
}

