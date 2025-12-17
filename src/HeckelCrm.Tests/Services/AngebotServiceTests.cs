using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using static HeckelCrm.Core.Interfaces.ILexofficeService;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace HeckelCrm.Tests.Services;

public class AngebotServiceTests
{
    private readonly Mock<IOfferRepository> _angebotRepositoryMock;
    private readonly Mock<IQuoteRequestRepository> _quoteRequestRepositoryMock;
    private readonly Mock<IContactRepository> _contactRepositoryMock;
    private readonly Mock<ILexofficeService> _lexofficeServiceMock;
    private readonly Mock<IApplicationTypeRepository> _applicationTypeRepositoryMock;
    private readonly Mock<ILogger<OfferService>> _loggerMock;
    private readonly OfferService _angebotService;

    public AngebotServiceTests()
    {
        _angebotRepositoryMock = new Mock<IOfferRepository>();
        _quoteRequestRepositoryMock = new Mock<IQuoteRequestRepository>();
        _applicationTypeRepositoryMock = new Mock<IApplicationTypeRepository>();
        _contactRepositoryMock = new Mock<IContactRepository>();
        _lexofficeServiceMock = new Mock<ILexofficeService>();
        _loggerMock = new Mock<ILogger<OfferService>>();
        
        _angebotService = new OfferService(
            _angebotRepositoryMock.Object,
            _quoteRequestRepositoryMock.Object,
            _contactRepositoryMock.Object,
            _lexofficeServiceMock.Object,
            _applicationTypeRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAngebotAsync_WithValidData_ShouldCreateAngebot()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var quoteRequestId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            LexofficeContactId = "lexoffice-123"
        };

        var quoteRequest = new QuoteRequest
        {
            Id = quoteRequestId,
            ContactId = contactId,
            Contact = contact,
            Status = RequestStatus.New
        };

        var createDto = new CreateOfferDto
        {   
            QuoteRequestIds = new List<Guid> { quoteRequestId },
            Title = "Test Angebot",
            Description = "Test Description",            
            Currency = "EUR",
            ValidUntil = DateTime.UtcNow.AddDays(30),
            LineItems = new List<OfferLineItemDto>
            {
                new OfferLineItemDto
                {
                    Name = "Test Item",
                    Description = "Test Description",
                    Quantity = 1,
                    UnitPrice = 1000.00m,
                    TaxRatePercentage = 19,
                    Days = 14
                }
            }
        };

        _quoteRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(quoteRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quoteRequest);

        _lexofficeServiceMock
            .Setup(s => s.CreateQuoteAsync(It.IsAny<QuoteData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("lexoffice-quote-123");

        _lexofficeServiceMock
            .Setup(s => s.GetQuoteAsync("lexoffice-quote-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuoteInfo("lexoffice-quote-123", "Q-001", "https://lexoffice.de/quote/123", DateTime.UtcNow, null, "active"));

        _angebotRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Offer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer angebot, CancellationToken ct) => angebot);

        // Act
        var result = await _angebotService.CreateOfferAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(createDto.Title);        
        result.Currency.Should().Be(createDto.Currency);
        result.Status.Should().Be("Created");

        _angebotRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Offer>(), It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task GetAllAngeboteAsync_ShouldReturnAllAngebote()
    {
        // Arrange
        var angebote = new List<Offer>
        {
            new Offer { Id = Guid.NewGuid(), Title = "Angebot 1", Amount = 1000m, Currency = "EUR" },
            new Offer { Id = Guid.NewGuid(), Title = "Angebot 2", Amount = 2000m, Currency = "EUR" }
        };

        _angebotRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(angebote);

        // Act
        var result = await _angebotService.GetAllOffersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAngebotByIdAsync_WithValidId_ShouldReturnAngebot()
    {
        // Arrange
        var angebotId = Guid.NewGuid();
        var quoteRequestId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com"
        };

        var quoteRequest = new QuoteRequest
        {
            Id = quoteRequestId,
            ContactId = contact.Id,
            Contact = contact
        };

        var angebot = new Offer
        {
            Id = angebotId,
            QuoteRequestId = quoteRequestId,
            QuoteRequest = quoteRequest,
            Title = "Test Angebot",
            Amount = 1000m,
            Currency = "EUR",
            Status = QuoteStatus.Created
        };

        _angebotRepositoryMock
            .Setup(r => r.GetByIdAsync(angebotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(angebot);

        // Act
        var result = await _angebotService.GetOfferByIdAsync(angebotId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(angebotId);
        result.Title.Should().Be("Test Angebot");
    }
}

