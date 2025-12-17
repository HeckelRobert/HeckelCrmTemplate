using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Web.Controllers;

[Authorize(Policy = "Admin")]
public class ApplicationTypesUiController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<ApplicationTypesUiController> _logger;

    public ApplicationTypesUiController(ApiClient apiClient, ILogger<ApplicationTypesUiController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var applicationTypes = await _apiClient.GetApplicationTypesAsync(cancellationToken);
        return View(applicationTypes ?? Enumerable.Empty<ApplicationTypeDto>());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateApplicationTypeDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApplicationTypeDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        try
        {
            var applicationType = await _apiClient.CreateApplicationTypeAsync(dto, cancellationToken);
            if (applicationType != null)
            {
                TempData["SuccessMessage"] = "Anwendungstyp erfolgreich erstellt.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Erstellen des Anwendungstyps.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating application type");
            TempData["ErrorMessage"] = $"Fehler beim Erstellen: {ex.Message}";
        }

        return View(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var applicationType = await _apiClient.GetApplicationTypeByIdAsync(id, cancellationToken);
        if (applicationType == null)
        {
            return NotFound();
        }

        var dto = new CreateApplicationTypeDto
        {
            Name = applicationType.Name,
            Description = applicationType.Description
        };

        ViewBag.Id = id;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CreateApplicationTypeDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Id = id;
            return View(dto);
        }

        try
        {
            var applicationType = await _apiClient.UpdateApplicationTypeAsync(id, dto, cancellationToken);
            if (applicationType != null)
            {
                TempData["SuccessMessage"] = "Anwendungstyp erfolgreich aktualisiert.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Aktualisieren des Anwendungstyps.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application type {Id}", id);
            TempData["ErrorMessage"] = $"Fehler beim Aktualisieren: {ex.Message}";
        }

        ViewBag.Id = id;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _apiClient.DeleteApplicationTypeAsync(id, cancellationToken);
            if (deleted)
            {
                TempData["SuccessMessage"] = "Anwendungstyp erfolgreich gelöscht.";
            }
            else
            {
                TempData["ErrorMessage"] = "Fehler beim Löschen des Anwendungstyps.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting application type {Id}", id);
            TempData["ErrorMessage"] = $"Fehler beim Löschen: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}

