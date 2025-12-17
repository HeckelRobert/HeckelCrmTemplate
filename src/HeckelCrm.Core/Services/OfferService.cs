using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace HeckelCrm.Core.Services;

public class OfferService : IOfferService
{
    private readonly IOfferRepository _offerRepository;
    private readonly IQuoteRequestRepository _quoteRequestRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILexofficeService _lexofficeService;
    private readonly IApplicationTypeRepository _applicationTypeRepository;
    private readonly ILogger<OfferService> _logger;

    public OfferService(
        IOfferRepository offerRepository,
        IQuoteRequestRepository quoteRequestRepository,
        IContactRepository contactRepository,
        ILexofficeService lexofficeService,
        IApplicationTypeRepository applicationTypeRepository,
        ILogger<OfferService> logger)
    {
        _offerRepository = offerRepository;
        _quoteRequestRepository = quoteRequestRepository;
        _contactRepository = contactRepository;
        _lexofficeService = lexofficeService;
        _applicationTypeRepository = applicationTypeRepository;
        _logger = logger;
    }

    public async Task<OfferDto> CreateOfferAsync(CreateOfferDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.QuoteRequestIds == null || dto.QuoteRequestIds.Count == 0)
        {
            throw new InvalidOperationException("At least one quote request ID is required.");
        }

        var quoteRequests = new List<QuoteRequest>();
        Contact? contact = null;

        foreach (var quoteRequestId in dto.QuoteRequestIds)
        {
            var quoteRequest = await _quoteRequestRepository.GetByIdAsync(quoteRequestId, cancellationToken);
            if (quoteRequest == null)
            {
                throw new InvalidOperationException($"Quote request with ID '{quoteRequestId}' not found.");
            }

            quoteRequests.Add(quoteRequest);

            if (contact == null)
            {
                contact = quoteRequest.Contact;
            }
            else if (quoteRequest.Contact?.Id != contact.Id)
            {
                throw new InvalidOperationException("All quote requests must belong to the same contact.");
            }
        }

        if (contact == null)
        {
            throw new InvalidOperationException("Contact not found for quote requests.");
        }

        if (string.IsNullOrEmpty(contact.LexofficeContactId))
        {
            throw new InvalidOperationException("Contact must be created in Lexoffice before creating a quote.");
        }

        var days = dto.LineItems?.Sum(_ => _.Days);

        // Generate title: "{Anwendungstyp} - {Name der ersten Leistung}"
        string title;
        if (dto.ApplicationTypeId.HasValue && dto.LineItems?.Any() == true)
        {
            var applicationType = await _applicationTypeRepository.GetByIdAsync(dto.ApplicationTypeId.Value, cancellationToken);
            var firstLineItemName = dto.LineItems.First().Name ?? string.Empty;
            title = applicationType != null
                ? $"{applicationType.Name} - {firstLineItemName}"
                : firstLineItemName;
        }
        else if (dto.LineItems?.Any() == true)
        {
            title = dto.LineItems.First().Name ?? string.Empty;
        }
        else
        {
            title = dto.Title ?? string.Empty;
        }

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            QuoteRequestId = dto.QuoteRequestIds.First(),
            Title = title,
            Description = dto.Description,
            Currency = dto.Currency,
            ValidUntil = dto.ValidUntil,
            Status = QuoteStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Days = days,
            BillingStatus = BillingStatus.New,
            ApplicationTypeId = dto.ApplicationTypeId
        };

        try
        {
            var lineItems = dto.LineItems?.Select(item => new QuoteLineItem(
                item.ArticleId,
                item.Name,
                item.ArticleType,
                item.Description,
                item.Quantity,
                item.UnitName,
                item.UnitPrice,
                item.TaxRatePercentage,
                item.Days
            )).ToList();

            var quoteData = new QuoteData(
                contact.LexofficeContactId,
                dto.Currency,
                dto.ValidUntil,
                lineItems
            );

            var quoteId = await _lexofficeService.CreateQuoteAsync(quoteData, cancellationToken);
            if (!string.IsNullOrEmpty(quoteId))
            {
                offer.LexofficeQuoteId = quoteId;
                offer.LexofficeCreatedAt = DateTime.UtcNow;

                var quoteInfo = await _lexofficeService.GetQuoteAsync(quoteId, cancellationToken);
                if (quoteInfo != null)
                {
                    offer.LexofficeQuoteNumber = quoteInfo.QuoteNumber;
                    offer.LexofficeQuoteLink = quoteInfo.Link;
                }
                else
                {
                    var quoteLink = await _lexofficeService.GetQuoteLinkAsync(quoteId, cancellationToken);
                    offer.LexofficeQuoteLink = quoteLink;
                }

                _logger.LogInformation("Created Lexoffice quote for Offer {OfferId}: {QuoteId}, Number: {QuoteNumber}",
                    offer.Id, quoteId, offer.LexofficeQuoteNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Lexoffice quote for offer {OfferId}", offer.Id);
            throw new InvalidOperationException($"Failed to create quote in Lexoffice: {ex.Message}", ex);
        }

        await _offerRepository.AddAsync(offer, cancellationToken);
        
        // Automatically update QuoteRequest status to QuoteCreated when an offer is created
        foreach (var quoteRequest in quoteRequests)
        {
            if (quoteRequest.Status == RequestStatus.New)
            {
                quoteRequest.Status = RequestStatus.QuoteCreated;
                quoteRequest.SelectedQuoteId = offer.Id;
                quoteRequest.UpdatedAt = DateTime.UtcNow;
                await _quoteRequestRepository.UpdateAsync(quoteRequest, cancellationToken);
                _logger.LogInformation("Updated QuoteRequest {QuoteRequestId} status to QuoteCreated after creating offer {OfferId}", 
                    quoteRequest.Id, offer.Id);
            }
        }
        
        return await MapToDtoAsync(offer, cancellationToken);
    }

    public async Task<OfferDto?> GetOfferByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null) return null;

        return await MapToDtoAsync(offer, cancellationToken);
    }

    public async Task<IEnumerable<OfferDto>> GetAllOffersAsync(CancellationToken cancellationToken = default)
    {
        var offers = await _offerRepository.GetAllAsync(cancellationToken);
        var result = new List<OfferDto>();
        foreach (var offer in offers)
        {
            result.Add(await MapToDtoAsync(offer, cancellationToken));
        }
        return result;
    }

    public async Task<IEnumerable<OfferDto>> GetOffersByQuoteRequestIdAsync(Guid quoteRequestId, CancellationToken cancellationToken = default)
    {
        var offers = await _offerRepository.GetByQuoteRequestIdAsync(quoteRequestId, cancellationToken);
        var result = new List<OfferDto>();
        foreach (var offer in offers)
        {
            result.Add(await MapToDtoAsync(offer, cancellationToken));
        }
        return result;
    }

    public async Task<IEnumerable<OfferDto>> GetOffersByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var offers = await _offerRepository.GetByContactIdAsync(contactId, cancellationToken);
        var result = new List<OfferDto>();
        foreach (var offer in offers)
        {
            result.Add(await MapToDtoAsync(offer, cancellationToken));
        }
        return result;
    }

    public async Task<IEnumerable<OfferDto>> GetOffersByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        var offers = await _offerRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        var result = new List<OfferDto>();
        foreach (var offer in offers)
        {
            result.Add(await MapToDtoAsync(offer, cancellationToken));
        }
        return result;
    }

    public async Task<OfferDto> UpdateOfferAsync(Guid id, CreateOfferDto dto, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            throw new InvalidOperationException($"Offer with ID '{id}' not found.");
        }

        offer.Title = dto.Title;
        offer.Description = dto.Description;
        offer.Currency = dto.Currency;
        offer.ValidUntil = dto.ValidUntil;
        offer.UpdatedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(offer, cancellationToken);
        return await MapToDtoAsync(offer, cancellationToken);
    }

    public async Task<OfferDto> UpdateOfferStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            throw new InvalidOperationException($"Offer with ID '{id}' not found.");
        }

        if (!Enum.TryParse<QuoteStatus>(status, out var newStatus))
        {
            throw new InvalidOperationException($"Invalid status '{status}'.");
        }

        offer.Status = newStatus;
        offer.UpdatedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(offer, cancellationToken);
        return await MapToDtoAsync(offer, cancellationToken);
    }

    public async Task<bool> DeleteOfferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null) return false;

        await _offerRepository.DeleteAsync(offer, cancellationToken);
        return true;
    }

    public async Task SyncOfferWithLexofficeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            throw new InvalidOperationException($"Offer with ID '{id}' not found.");
        }

        if (string.IsNullOrEmpty(offer.LexofficeQuoteId))
        {
            _logger.LogWarning("Offer {OfferId} does not have a Lexoffice quote ID. Cannot sync with Lexoffice.", id);
            throw new InvalidOperationException($"Offer does not have a Lexoffice quote ID. Please create the quote in Lexoffice first.");
        }

        try
        {
            var quoteInfo = await _lexofficeService.GetQuoteAsync(offer.LexofficeQuoteId, cancellationToken);
            if (quoteInfo != null)
            {
                offer.LexofficeQuoteNumber = quoteInfo.QuoteNumber ?? offer.LexofficeQuoteNumber;
                offer.LexofficeQuoteLink = quoteInfo.Link;
                offer.LexofficeVoucherStatus = quoteInfo.VoucherStatus;
                offer.UpdatedAt = DateTime.UtcNow;

                // Update status based on Lexoffice status
                if (quoteInfo.Status == "accepted" || quoteInfo.Status == "archived")
                {
                    offer.Status = QuoteStatus.InProgress;
                    if (!offer.ClientAcceptedAt.HasValue)
                    {
                        offer.ClientAcceptedAt = DateTime.UtcNow;
                    }
                    if (offer.LexofficeCreatedAt.HasValue)
                    {
                        offer.DaysUntilAcceptance = (int)(DateTime.UtcNow - offer.LexofficeCreatedAt.Value).TotalDays;
                    }
                }
                else if (quoteInfo.Status == "rejected")
                {
                    offer.Status = QuoteStatus.Rejected;
                }

                await _offerRepository.UpdateAsync(offer, cancellationToken);
                _logger.LogInformation("Synced offer {OfferId} with Lexoffice", id);
            }
            else
            {
                // Quote was deleted in Lexoffice - remove Lexoffice references
                _logger.LogWarning("Quote {QuoteId} not found in Lexoffice (deleted). Removing Lexoffice references from offer {OfferId}.", 
                    offer.LexofficeQuoteId, id);
                
                offer.LexofficeQuoteId = null;
                offer.LexofficeQuoteNumber = null;
                offer.LexofficeQuoteLink = null;
                offer.LexofficeVoucherStatus = null;
                offer.UpdatedAt = DateTime.UtcNow;
                
                await _offerRepository.UpdateAsync(offer, cancellationToken);
                _logger.LogInformation("Removed Lexoffice references from offer {OfferId} because quote was deleted in Lexoffice", id);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
        {
            // Quote was deleted in Lexoffice - remove Lexoffice references
            _logger.LogWarning("Quote {QuoteId} not found in Lexoffice (404). Removing Lexoffice references from offer {OfferId}.", 
                offer.LexofficeQuoteId, id);
            
            offer.LexofficeQuoteId = null;
            offer.LexofficeQuoteNumber = null;
            offer.LexofficeQuoteLink = null;
            offer.LexofficeVoucherStatus = null;
            offer.UpdatedAt = DateTime.UtcNow;
            
            await _offerRepository.UpdateAsync(offer, cancellationToken);
            _logger.LogInformation("Removed Lexoffice references from offer {OfferId} because quote was deleted in Lexoffice", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync offer {OfferId} with Lexoffice", id);
            throw;
        }
    }

    public async Task<IEnumerable<OfferDto>> LoadOffersFromLexofficeAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(contactId, cancellationToken);
        if (contact == null)
        {
            throw new InvalidOperationException($"Contact with ID '{contactId}' not found.");
        }

        if (string.IsNullOrEmpty(contact.LexofficeContactId))
        {
            throw new InvalidOperationException($"Contact {contactId} does not have a Lexoffice contact ID. Cannot load quotes from Lexoffice.");
        }

        try
        {
            // Load quotes from Lexoffice for this contact
            var lexofficeQuotes = await _lexofficeService.GetQuotesByContactIdAsync(contact.LexofficeContactId, cancellationToken);

            var result = new List<OfferDto>();

            foreach (var lexofficeQuote in lexofficeQuotes)
            {
                // Check if we already have this quote in our database
                var existingOffer = await _offerRepository.GetByLexofficeQuoteIdAsync(lexofficeQuote.Id, cancellationToken);

                if (existingOffer != null)
                {
                    // Update existing offer with latest info from Lexoffice
                    existingOffer.LexofficeQuoteNumber = lexofficeQuote.QuoteNumber;
                    existingOffer.LexofficeQuoteLink = lexofficeQuote.Link;
                    existingOffer.ValidUntil = lexofficeQuote.ValidUntil;
                    existingOffer.UpdatedAt = DateTime.UtcNow;

                    // Update status based on Lexoffice status
                    if (lexofficeQuote.Status == "archived")
                    {
                        existingOffer.Status = QuoteStatus.InProgress;
                    }
                    else if (lexofficeQuote.Status == "rejected")
                    {
                        existingOffer.Status = QuoteStatus.Rejected;
                    }

                    await _offerRepository.UpdateAsync(existingOffer, cancellationToken);
                    result.Add(await MapToDtoAsync(existingOffer, cancellationToken));
                }
                else
                {
                    // Create a new Offer from Lexoffice quote
                    // We need to find or create a QuoteRequest for this contact
                    var quoteRequests = await _quoteRequestRepository.GetByContactIdAsync(contactId, cancellationToken);
                    var quoteRequest = quoteRequests.FirstOrDefault(qr => qr.Status == RequestStatus.New)
                        ?? quoteRequests.FirstOrDefault();

                    if (quoteRequest == null)
                    {
                        // Create a default quote request if none exists
                        quoteRequest = new QuoteRequest
                        {
                            Id = Guid.NewGuid(),
                            ContactId = contactId,
                            Requirements = $"Automatisch erstellt aus Lexoffice Offer {lexofficeQuote.QuoteNumber}",
                            Status = RequestStatus.QuoteCreated,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _quoteRequestRepository.AddAsync(quoteRequest, cancellationToken);
                    }

                    var newOffer = new Offer
                    {
                        Id = Guid.NewGuid(),
                        QuoteRequestId = quoteRequest.Id,
                        Title = lexofficeQuote.QuoteNumber ?? "Offer aus Lexoffice",
                        Description = $"Offer aus Lexoffice geladen",
                        Amount = 0, // We don't have amount info from the list endpoint
                        Currency = "EUR",
                        Status = lexofficeQuote.Status == "archived" ? QuoteStatus.InProgress : QuoteStatus.Created,
                        CreatedAt = lexofficeQuote.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        LexofficeQuoteId = lexofficeQuote.Id,
                        LexofficeQuoteNumber = lexofficeQuote.QuoteNumber,
                        LexofficeQuoteLink = lexofficeQuote.Link,
                        LexofficeCreatedAt = lexofficeQuote.CreatedAt,
                        ValidUntil = lexofficeQuote.ValidUntil,
                        BillingStatus = BillingStatus.New
                    };

                    await _offerRepository.AddAsync(newOffer, cancellationToken);
                    result.Add(await MapToDtoAsync(newOffer, cancellationToken));
                }
            }

            _logger.LogInformation("Loaded {Count} quotes from Lexoffice for contact {ContactId}", result.Count, contactId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load quotes from Lexoffice for contact {ContactId}", contactId);
            throw;
        }
    }

    private async Task<OfferDto> MapToDtoAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var contact = offer.QuoteRequest?.Contact;

        // Calculate days until acceptance if accepted
        int? daysUntilAcceptance = null;
        if (offer.Status == QuoteStatus.InProgress && offer.LexofficeCreatedAt.HasValue && offer.ClientAcceptedAt.HasValue)
        {
            daysUntilAcceptance = (int)(offer.ClientAcceptedAt.Value - offer.LexofficeCreatedAt.Value).TotalDays;
        }
        else if (offer.LexofficeCreatedAt.HasValue && offer.Status != QuoteStatus.InProgress)
        {
            // Calculate days since creation if not yet accepted
            daysUntilAcceptance = (int)(DateTime.UtcNow - offer.LexofficeCreatedAt.Value).TotalDays;
        }

        // Prepare Lexoffice-related data (voucher status + line items)
        List<OfferLineItemDto>? lineItems = null;
        string? lexofficeVoucherStatus = offer.LexofficeVoucherStatus;
        if (!string.IsNullOrEmpty(offer.LexofficeQuoteId))
        {
            try
            {
                var quoteInfo = await _lexofficeService.GetQuoteAsync(offer.LexofficeQuoteId, cancellationToken);

                // Always take the latest voucher status from Lexoffice if available
                if (!string.IsNullOrEmpty(quoteInfo?.VoucherStatus))
                {
                    lexofficeVoucherStatus = quoteInfo.VoucherStatus;
                }

                if (quoteInfo?.LineItems != null)
                {
                    lineItems = quoteInfo.LineItems.Select(item => new OfferLineItemDto
                    {
                        ArticleId = item.ArticleId ?? string.Empty,
                        Name = item.Name ?? string.Empty,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitName = item.UnitName ?? "Tage",
                        UnitPrice = item.UnitPrice ?? 0,
                        TaxRatePercentage = item.TaxRatePercentage ?? 19,
                        Days = (int)item.Quantity // Use quantity as days
                    }).ToList();
                }
            }
            catch
            {
                // If we can't fetch line items, continue without them
                lineItems = null;
            }
        }

        // Get ApplicationType name if available
        string? applicationTypeName = null;
        if (offer.ApplicationTypeId.HasValue)
        {
            var applicationType = await _applicationTypeRepository.GetByIdAsync(offer.ApplicationTypeId.Value, cancellationToken);
            applicationTypeName = applicationType?.Name;
        }

        // Calculate total days from line items if available, otherwise use offer.Days
        int? calculatedDays = null;
        if (lineItems != null && lineItems.Any())
        {
            calculatedDays = lineItems.Sum(item => item.Days);
        }
        else
        {
            calculatedDays = offer.Days;
        }

        return new OfferDto
        {
            Id = offer.Id,
            QuoteRequestId = offer.QuoteRequestId,
            ContactId = contact?.Id ?? Guid.Empty,
            ContactName = contact != null ? $"{contact.FirstName} {contact.LastName}" : string.Empty,
            ContactEmail = contact?.Email ?? string.Empty,
            Title = offer.Title,
            Description = offer.Description,
            Amount = offer.Amount,
            Currency = offer.Currency,
            Status = offer.Status.ToString(),
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt,
            ValidUntil = offer.ValidUntil,
            LexofficeQuoteId = offer.LexofficeQuoteId,
            LexofficeQuoteNumber = offer.LexofficeQuoteNumber,
            LexofficeQuoteLink = offer.LexofficeQuoteLink,
            LexofficeCreatedAt = offer.LexofficeCreatedAt,
            ClientAcceptedAt = offer.ClientAcceptedAt,
            DaysUntilAcceptance = daysUntilAcceptance,
            Days = calculatedDays,
            BillingStatus = offer.BillingStatus.ToString(),
            LexofficeVoucherStatus = lexofficeVoucherStatus,
            LineItems = lineItems,
            ApplicationTypeId = offer.ApplicationTypeId,
            ApplicationTypeName = applicationTypeName
        };
    }

    public async Task<OfferDto> UpdateBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(id, cancellationToken);
        if (offer == null)
        {
            throw new InvalidOperationException($"Offer with ID '{id}' not found.");
        }

        if (!Enum.TryParse<BillingStatus>(dto.BillingStatus, out var newStatus))
        {
            throw new InvalidOperationException($"Invalid billing status '{dto.BillingStatus}'.");
        }

        var currentStatus = offer.BillingStatus;

        // Only allow setting to Billed when Lexoffice status is accepted
        if (newStatus == BillingStatus.Billed &&
            !string.Equals(offer.LexofficeVoucherStatus, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Der Abrechnungsstatus kann nur auf 'Abgerechnet' gesetzt werden, wenn der Lexoffice-Status 'Angenommen' ist.");
        }

        // Validate status transitions based on permissions
        if (!CanChangeBillingStatus(currentStatus, newStatus, isAdmin))
        {
            throw new InvalidOperationException(
                $"Cannot change billing status from {currentStatus} to {newStatus}. " +
                $"{(isAdmin ? "Admin" : "Partner")} permissions do not allow this transition.");
        }

        offer.BillingStatus = newStatus;
        offer.UpdatedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(offer, cancellationToken);

        return await MapToDtoAsync(offer, cancellationToken);
    }

    private static bool CanChangeBillingStatus(BillingStatus currentStatus, BillingStatus newStatus, bool isAdmin)
    {
        // Status cannot be changed to the same status
        if (currentStatus == newStatus)
        {
            return false;
        }

        // Once billed, cannot go back to New
        if (currentStatus == BillingStatus.Billed && newStatus == BillingStatus.New)
        {
            return false;
        }

        // Partner can only change: New -> Billed
        if (!isAdmin)
        {
            // Partners cannot go back from Paid
            if (currentStatus == BillingStatus.Paid)
            {
                return false;
            }
            return currentStatus == BillingStatus.New && newStatus == BillingStatus.Billed;
        }

        // Admin can change: New -> Billed, Billed -> Paid, Paid -> Billed (for corrections)
        return (currentStatus == BillingStatus.New && newStatus == BillingStatus.Billed) ||
               (currentStatus == BillingStatus.Billed && newStatus == BillingStatus.Paid) ||
               (currentStatus == BillingStatus.Paid && newStatus == BillingStatus.Billed);
    }

    public async Task<int> BatchSyncOpenOffersWithLexofficeAsync(CancellationToken cancellationToken = default)
    {
        // Get all offers that have a Lexoffice quote ID (sync all offers, not just open ones)
        var allOffers = await _offerRepository.GetAllAsync(cancellationToken);
        var offersWithLexofficeId = allOffers
            .Where(a => !string.IsNullOrEmpty(a.LexofficeQuoteId))
            .ToList();

        var syncedCount = 0;
        var deletedCount = 0;
        foreach (var offer in offersWithLexofficeId)
        {
            try
            {
                var lexofficeQuoteIdBefore = offer.LexofficeQuoteId;
                await SyncOfferWithLexofficeAsync(offer.Id, cancellationToken);
                
                // Check if the quote was deleted (LexofficeQuoteId was removed)
                var offerAfterSync = await _offerRepository.GetByIdAsync(offer.Id, cancellationToken);
                if (offerAfterSync != null && string.IsNullOrEmpty(offerAfterSync.LexofficeQuoteId) && !string.IsNullOrEmpty(lexofficeQuoteIdBefore))
                {
                    deletedCount++;
                }
                
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync offer {OfferId} in batch sync", offer.Id);
                // Continue with next offer
            }
        }

        _logger.LogInformation("Batch synced {Count} out of {Total} offers with Lexoffice. {DeletedCount} offers were deleted in Lexoffice.", 
            syncedCount, offersWithLexofficeId.Count, deletedCount);
        return syncedCount;
    }
}
