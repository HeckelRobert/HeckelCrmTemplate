using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeckelCrm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Partner")]
public class OffersController : ControllerBase
{
    private readonly IOfferService _offerService;
    private readonly ILexofficeService _lexofficeService;
    private readonly ILogger<OffersController> _logger;

    public OffersController(
        IOfferService offerService,
        ILexofficeService lexofficeService,
        ILogger<OffersController> logger)
    {
        _offerService = offerService;
        _lexofficeService = lexofficeService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")] // Only admins can see all Offers
    public async Task<ActionResult<IEnumerable<OfferDto>>> GetAllOffers(CancellationToken cancellationToken)
    {
        var Offers = await _offerService.GetAllOffersAsync(cancellationToken);
        return Ok(Offers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OfferDto>> GetOfferById(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _offerService.GetOfferByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            return NotFound();
        }
        return Ok(offer);
    }

    [HttpGet("quote-request/{quoteRequestId}")]
    public async Task<ActionResult<IEnumerable<OfferDto>>> GetOffersByQuoteRequestId(Guid quoteRequestId, CancellationToken cancellationToken)
    {
        var Offers = await _offerService.GetOffersByQuoteRequestIdAsync(quoteRequestId, cancellationToken);
        return Ok(Offers);
    }

    [HttpGet("contact/{contactId}")]
    public async Task<ActionResult<IEnumerable<OfferDto>>> GetOffersByContactId(Guid contactId, CancellationToken cancellationToken)
    {
        var Offers = await _offerService.GetOffersByContactIdAsync(contactId, cancellationToken);
        return Ok(Offers);
    }

    [HttpGet("partner/{partnerId}")]
    public async Task<ActionResult<IEnumerable<OfferDto>>> GetOffersByPartnerId(string partnerId, CancellationToken cancellationToken)
    {
        var Offers = await _offerService.GetOffersByPartnerIdAsync(partnerId, cancellationToken);
        return Ok(Offers);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")] // Only admins can create Offers
    public async Task<ActionResult<OfferDto>> CreateOffer([FromBody] CreateOfferDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var offer = await _offerService.CreateOfferAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetOfferById), new { id = offer.Id }, offer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Admin")] // Only admins can update Offers
    public async Task<ActionResult<OfferDto>> UpdateOffer(Guid id, [FromBody] CreateOfferDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var offer = await _offerService.UpdateOfferAsync(id, dto, cancellationToken);
            return Ok(offer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "Admin")] // Only admins can update Offer status
    public async Task<ActionResult<OfferDto>> UpdateOfferStatus(Guid id, [FromBody] UpdateOfferStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var offer = await _offerService.UpdateOfferStatusAsync(id, dto.Status, cancellationToken);
            return Ok(offer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/sync-lexoffice")]
    public async Task<ActionResult<OfferDto>> SyncOfferWithLexoffice(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _offerService.SyncOfferWithLexofficeAsync(id, cancellationToken);
            var offer = await _offerService.GetOfferByIdAsync(id, cancellationToken);
            return Ok(offer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("contact/{contactId}/load-from-lexoffice")]
    [Authorize(Policy = "Admin")] // Only admins can load quotes from Lexoffice
    public async Task<ActionResult<IEnumerable<OfferDto>>> LoadOffersFromLexoffice(Guid contactId, CancellationToken cancellationToken)
    {
        try
        {
            var offers = await _offerService.LoadOffersFromLexofficeAsync(contactId, cancellationToken);
            return Ok(offers);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Offers from Lexoffice for contact {ContactId}", contactId);
            return StatusCode(500, "Fehler beim Laden der Offers aus Lexoffice.");
        }
    }

    [HttpGet("articles")]
    [Authorize(Policy = "Admin")] // Only admins can load articles
    public async Task<ActionResult<IEnumerable<ArticleInfo>>> GetArticles(CancellationToken cancellationToken)
    {
        try
        {
            var articles = await _lexofficeService.GetArticlesAsync(cancellationToken);
            return Ok(articles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading articles from Lexoffice");
            // Return empty list instead of error to allow UI to continue working
            return Ok(Enumerable.Empty<ArticleInfo>());
        }
    }

    [HttpPatch("{id}/billing-status")]
    public async Task<ActionResult<OfferDto>> UpdateBillingStatus(Guid id, [FromBody] UpdateBillingStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var isAdmin = User.IsInRole("Admin");
            var offer = await _offerService.UpdateBillingStatusAsync(id, dto, isAdmin, cancellationToken);
            return Ok(offer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("batch-sync")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<int>> BatchSyncOpenOffers(CancellationToken cancellationToken)
    {
        try
        {
            var syncedCount = await _offerService.BatchSyncOpenOffersWithLexofficeAsync(cancellationToken);
            return Ok(syncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch syncing Offers with Lexoffice");
            return StatusCode(500, "Fehler beim Synchronisieren der Offers.");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteOffer(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _offerService.DeleteOfferAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}

