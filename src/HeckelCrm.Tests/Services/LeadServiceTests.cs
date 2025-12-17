using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Services;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace HeckelCrm.Tests.Services;

public class LeadServiceTests
{
    private readonly Mock<ILeadRepository> _leadRepositoryMock;
    private readonly Mock<IPartnerRepository> _partnerRepositoryMock;
    private readonly Mock<ILexofficeService> _lexofficeServiceMock;
    private readonly Mock<ILogger<LeadService>> _loggerMock;
    private readonly LeadService _leadService;

    public LeadServiceTests()
    {
        _leadRepositoryMock = new Mock<ILeadRepository>();
        _partnerRepositoryMock = new Mock<IPartnerRepository>();
        _lexofficeServiceMock = new Mock<ILexofficeService>();
        _loggerMock = new Mock<ILogger<LeadService>>();
        
        _leadService = new LeadService(
            _leadRepositoryMock.Object,
            _partnerRepositoryMock.Object,
            _lexofficeServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateLeadAsync_WithValidData_ShouldCreateLead()
    {
        // Arrange
        var partnerId = "partner-123";
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            PartnerId = partnerId,
            Name = "Test Partner",
            Email = "partner@test.com"
        };

        var createDto = new CreateLeadDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@test.com",
            Phone = "+1234567890",
            CompanyName = "Test Company",
            PartnerId = partnerId,
            PrivacyPolicyAccepted = true,
            TermsAccepted = true,
            DataProcessingAccepted = true
        };

        _partnerRepositoryMock
            .Setup(r => r.GetByPartnerIdAsync(partnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _leadRepositoryMock
            .Setup(r => r.GetByEmailAsync(createDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        _leadRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead lead, CancellationToken ct) => lead);

        // Act
        var result = await _leadService.CreateLeadAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be(createDto.FirstName);
        result.LastName.Should().Be(createDto.LastName);
        result.Email.Should().Be(createDto.Email);
        result.PartnerId.Should().Be(partnerId);
        result.Status.Should().Be("New");

        _leadRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLeadAsync_WithNonExistentPartner_ShouldThrowException()
    {
        // Arrange
        var createDto = new CreateLeadDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@test.com",
            PartnerId = "non-existent-partner",
            PrivacyPolicyAccepted = true,
            TermsAccepted = true,
            DataProcessingAccepted = true
        };

        _partnerRepositoryMock
            .Setup(r => r.GetByPartnerIdAsync(createDto.PartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Partner?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _leadService.CreateLeadAsync(createDto));
    }

    [Fact]
    public async Task CreateLeadAsync_WithExistingEmail_ShouldThrowException()
    {
        // Arrange
        var partnerId = "partner-123";
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            PartnerId = partnerId,
            Name = "Test Partner"
        };

        var existingLead = new Lead
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            PartnerId = partnerId
        };

        var createDto = new CreateLeadDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "existing@test.com",
            PartnerId = partnerId,
            PrivacyPolicyAccepted = true,
            TermsAccepted = true,
            DataProcessingAccepted = true
        };

        _partnerRepositoryMock
            .Setup(r => r.GetByPartnerIdAsync(partnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _leadRepositoryMock
            .Setup(r => r.GetByEmailAsync(createDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLead);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _leadService.CreateLeadAsync(createDto));
    }

    [Fact]
    public async Task GetAllLeadsAsync_ShouldReturnAllLeads()
    {
        // Arrange
        var leads = new List<Lead>
        {
            new Lead { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe", Email = "john@test.com" },
            new Lead { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Smith", Email = "jane@test.com" }
        };

        _leadRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(leads);

        // Act
        var result = await _leadService.GetAllLeadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLeadByIdAsync_WithValidId_ShouldReturnLead()
    {
        // Arrange
        var leadId = Guid.NewGuid();
        var lead = new Lead
        {
            Id = leadId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com"
        };

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        // Act
        var result = await _leadService.GetLeadByIdAsync(leadId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(leadId);
        result.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetLeadByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var leadId = Guid.NewGuid();

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        // Act
        var result = await _leadService.GetLeadByIdAsync(leadId);

        // Assert
        result.Should().BeNull();
    }
}

