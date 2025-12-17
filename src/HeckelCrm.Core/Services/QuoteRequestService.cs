using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class QuoteRequestService : IQuoteRequestService
{
    private readonly IQuoteRequestRepository _quoteRequestRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly ILogger<QuoteRequestService> _logger;

    public QuoteRequestService(
        IQuoteRequestRepository quoteRequestRepository,
        IContactRepository contactRepository,
        IOfferRepository offerRepository,
        ILogger<QuoteRequestService> logger)
    {
        _quoteRequestRepository = quoteRequestRepository;
        _contactRepository = contactRepository;
        _offerRepository = offerRepository;
        _logger = logger;
    }

    public async Task<QuoteRequestDto> CreateQuoteRequestAsync(CreateQuoteRequestDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating QuoteRequest for contact {ContactId}", dto.ContactId);
        
        var contact = await _contactRepository.GetByIdAsync(dto.ContactId, cancellationToken);
        if (contact == null)
        {
            _logger.LogError("Contact with ID '{ContactId}' not found.", dto.ContactId);
            throw new InvalidOperationException($"Contact with ID '{dto.ContactId}' not found.");
        }

        var quoteRequest = new QuoteRequest
        {
            Id = Guid.NewGuid(),
            ContactId = dto.ContactId,
            Requirements = dto.Requirements,
            Status = RequestStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Adding QuoteRequest {QuoteRequestId} to repository", quoteRequest.Id);
        var created = await _quoteRequestRepository.AddAsync(quoteRequest, cancellationToken);
        _logger.LogInformation("QuoteRequest {QuoteRequestId} added to repository. Reloading with navigation properties.", created.Id);
        
        // Reload with navigation properties to ensure Contact is loaded
        var reloaded = await _quoteRequestRepository.GetByIdAsync(created.Id, cancellationToken);
        if (reloaded == null)
        {
            _logger.LogError("QuoteRequest {QuoteRequestId} was created but could not be reloaded from database.", created.Id);
            throw new InvalidOperationException($"QuoteRequest {created.Id} was created but could not be reloaded from database.");
        }
        
        _logger.LogInformation("Successfully created and reloaded QuoteRequest {QuoteRequestId} for contact {ContactId}", reloaded.Id, dto.ContactId);
        return MapToDto(reloaded);
    }

    public async Task<QuoteRequestDto?> GetQuoteRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quoteRequest = await _quoteRequestRepository.GetByIdAsync(id, cancellationToken);
        if (quoteRequest == null) return null;

        return MapToDto(quoteRequest);
    }

    public async Task<IEnumerable<QuoteRequestDto>> GetAllQuoteRequestsAsync(CancellationToken cancellationToken = default)
    {
        var quoteRequests = await _quoteRequestRepository.GetAllAsync(cancellationToken);
        return quoteRequests.Select(MapToDto);
    }

    public async Task<IEnumerable<QuoteRequestDto>> GetQuoteRequestsByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var quoteRequests = await _quoteRequestRepository.GetByContactIdAsync(contactId, cancellationToken);
        return quoteRequests.Select(MapToDto);
    }

    public async Task<QuoteRequestDto> UpdateRequestStatusAsync(Guid id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default)
    {
        var quoteRequest = await _quoteRequestRepository.GetByIdAsync(id, cancellationToken);
        if (quoteRequest == null)
        {
            throw new InvalidOperationException($"Quote request with ID '{id}' not found.");
        }

        if (!Enum.TryParse<RequestStatus>(dto.Status, out var newStatus))
        {
            throw new InvalidOperationException($"Invalid request status '{dto.Status}'.");
        }

        var currentStatus = quoteRequest.Status;

        // Validate status transitions
        if (!CanChangeRequestStatus(currentStatus, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot change request status from {currentStatus} to {newStatus}.");
        }

        // If status is QuoteCreated, SelectedQuoteId must be provided
        if (newStatus == RequestStatus.QuoteCreated)
        {
            if (!dto.SelectedQuoteId.HasValue)
            {
                throw new InvalidOperationException("SelectedQuoteId is required when status is set to QuoteCreated.");
            }

            // Verify the quote exists and belongs to this quote request
            var quote = await _offerRepository.GetByIdAsync(dto.SelectedQuoteId.Value, cancellationToken);
            if (quote == null)
            {
                throw new InvalidOperationException($"Quote with ID '{dto.SelectedQuoteId.Value}' not found.");
            }

            if (quote.QuoteRequestId != id)
            {
                throw new InvalidOperationException($"Quote {dto.SelectedQuoteId.Value} does not belong to quote request {id}.");
            }

            quoteRequest.SelectedQuoteId = dto.SelectedQuoteId.Value;
        }
        else
        {
            // Clear selected quote if status is not QuoteCreated
            quoteRequest.SelectedQuoteId = null;
        }

        quoteRequest.Status = newStatus;
        quoteRequest.UpdatedAt = DateTime.UtcNow;

        await _quoteRequestRepository.UpdateAsync(quoteRequest, cancellationToken);

        // Reload with navigation properties to ensure Contact is loaded
        var reloaded = await _quoteRequestRepository.GetByIdAsync(id, cancellationToken);
        return MapToDto(reloaded ?? quoteRequest);
    }

    private static bool CanChangeRequestStatus(RequestStatus currentStatus, RequestStatus newStatus)
    {
        // Status cannot be changed to the same status
        if (currentStatus == newStatus)
        {
            return false;
        }

        // Can change from any status to any other status (admin can manage freely)
        // But we enforce that QuoteCreated requires a SelectedQuoteId (checked in UpdateRequestStatusAsync)
        return true;
    }

    public async Task<bool> DeleteQuoteRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quoteRequest = await _quoteRequestRepository.GetByIdAsync(id, cancellationToken);
        if (quoteRequest == null) return false;

        await _quoteRequestRepository.DeleteAsync(quoteRequest, cancellationToken);
        return true;
    }

    private static QuoteRequestDto MapToDto(QuoteRequest quoteRequest)
    {
        return new QuoteRequestDto
        {
            Id = quoteRequest.Id,
            ContactId = quoteRequest.ContactId,
            ContactName = quoteRequest.Contact != null 
                ? $"{quoteRequest.Contact.FirstName} {quoteRequest.Contact.LastName}".Trim() 
                : string.Empty,
            ContactEmail = quoteRequest.Contact?.Email ?? string.Empty,
            Requirements = quoteRequest.Requirements,
            Status = quoteRequest.Status.ToString(),
            CreatedAt = quoteRequest.CreatedAt,
            UpdatedAt = quoteRequest.UpdatedAt,
            SelectedQuoteId = quoteRequest.SelectedQuoteId,
            SelectedQuoteTitle = quoteRequest.SelectedQuote?.Title,
            AngeboteCount = quoteRequest.Offers?.Count ?? 0
        };
    }
}

