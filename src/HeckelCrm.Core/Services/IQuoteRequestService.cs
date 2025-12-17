using HeckelCrm.Core.DTOs;

namespace HeckelCrm.Core.Services;

public interface IQuoteRequestService
{
    Task<QuoteRequestDto> CreateQuoteRequestAsync(CreateQuoteRequestDto dto, CancellationToken cancellationToken = default);
    Task<QuoteRequestDto?> GetQuoteRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<QuoteRequestDto>> GetAllQuoteRequestsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<QuoteRequestDto>> GetQuoteRequestsByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<QuoteRequestDto> UpdateRequestStatusAsync(Guid id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteQuoteRequestAsync(Guid id, CancellationToken cancellationToken = default);
}

