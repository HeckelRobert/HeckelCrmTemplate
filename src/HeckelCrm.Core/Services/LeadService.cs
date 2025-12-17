using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class LeadService : ILeadService
{
    private readonly ILeadRepository _leadRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly ILexofficeService _lexofficeService;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ILeadRepository leadRepository,
        IPartnerRepository partnerRepository,
        ILexofficeService lexofficeService,
        ILogger<LeadService> logger)
    {
        _leadRepository = leadRepository;
        _partnerRepository = partnerRepository;
        _lexofficeService = lexofficeService;
        _logger = logger;
    }

    public async Task<LeadDto> CreateLeadAsync(CreateLeadDto dto, CancellationToken cancellationToken = default)
    {
        // Verify partner exists if PartnerId is provided
        Partner? partner = null;
        if (!string.IsNullOrEmpty(dto.PartnerId))
        {
            partner = await _partnerRepository.GetByPartnerIdAsync(dto.PartnerId, cancellationToken);
            if (partner == null)
            {
                throw new InvalidOperationException($"Partner with ID '{dto.PartnerId}' not found.");
            }
        }

        // Check if lead already exists - if so, update it instead of creating a new one
        var existingLead = await _leadRepository.GetByEmailAsync(dto.Email, cancellationToken);
        if (existingLead != null)
        {
            _logger.LogInformation("Lead with email '{Email}' already exists. Updating existing lead {LeadId}.", dto.Email, existingLead.Id);
            // Update existing lead instead of creating a new one
            return await UpdateLeadAsync(existingLead.Id, dto, cancellationToken);
        }

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            PartnerId = dto.PartnerId,
            CreatedAt = DateTime.UtcNow,
            Status = LeadStatus.New,
            PrivacyPolicyAccepted = dto.PrivacyPolicyAccepted,
            PrivacyPolicyAcceptedAt = DateTime.UtcNow,
            TermsAccepted = dto.TermsAccepted,
            TermsAcceptedAt = dto.TermsAccepted ? DateTime.UtcNow : default,
            DataProcessingAccepted = dto.DataProcessingAccepted,
            DataProcessingAcceptedAt = dto.DataProcessingAccepted ? DateTime.UtcNow : default,
            
            // Company Information
            CompanyName = dto.CompanyName,
            CompanyTaxNumber = dto.CompanyTaxNumber,
            CompanyVatRegistrationId = dto.CompanyVatRegistrationId,
            CompanyAllowTaxFreeInvoices = dto.CompanyAllowTaxFreeInvoices,
            
            // Billing Address
            BillingStreet = dto.BillingStreet,
            BillingZip = dto.BillingZip,
            BillingCity = dto.BillingCity,
            BillingCountryCode = dto.BillingCountryCode ?? "DE",
            BillingSupplement = dto.BillingSupplement,
            
            // Shipping Address
            ShippingStreet = dto.ShippingStreet,
            ShippingZip = dto.ShippingZip,
            ShippingCity = dto.ShippingCity,
            ShippingCountryCode = dto.ShippingCountryCode ?? "DE",
            ShippingSupplement = dto.ShippingSupplement,
            
            // Email Addresses
            EmailBusiness = dto.EmailBusiness ?? dto.Email,
            EmailOffice = dto.EmailOffice,
            EmailPrivate = dto.EmailPrivate,
            EmailOther = dto.EmailOther,
            
            // Phone Numbers
            PhoneBusiness = dto.PhoneBusiness ?? dto.Phone,
            PhoneOffice = dto.PhoneOffice,
            PhoneMobile = dto.PhoneMobile,
            PhonePrivate = dto.PhonePrivate,
            PhoneFax = dto.PhoneFax,
            PhoneOther = dto.PhoneOther,
            
            // Requirements/Notes
            Notes = dto.Requirements,
            
            // Salutation
            Salutation = dto.Salutation
        };

        // Create contact in Lexoffice (optional - only if configured)
        // Check if contact already exists by email, if not create new one
        try
        {
            var contactData = new ContactData(
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.Phone,
                dto.CompanyName,
                dto.CompanyTaxNumber,
                dto.CompanyVatRegistrationId,
                dto.CompanyAllowTaxFreeInvoices,
                dto.BillingStreet,
                dto.BillingZip,
                dto.BillingCity,
                dto.BillingCountryCode ?? "DE",
                dto.BillingSupplement,
                dto.ShippingStreet,
                dto.ShippingZip,
                dto.ShippingCity,
                dto.ShippingCountryCode ?? "DE",
                dto.ShippingSupplement,
                dto.EmailBusiness ?? dto.Email,
                dto.EmailOffice,
                dto.EmailPrivate,
                dto.EmailOther,
                dto.PhoneBusiness ?? dto.Phone,
                dto.PhoneOffice,
                dto.PhoneMobile,
                dto.PhonePrivate,
                dto.PhoneFax,
                dto.PhoneOther,
                dto.Salutation,
                dto.PartnerId // Use PartnerId for note field
            );
            var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
            if (!string.IsNullOrEmpty(lexofficeContactId))
            {
                lead.LexofficeContactId = lexofficeContactId;
                _logger.LogInformation("Created Lexoffice contact for lead {LeadId}: {ContactId}", lead.Id, lexofficeContactId);
            }
            else
            {
                _logger.LogDebug("Lexoffice is not configured or contact creation returned empty. Skipping Lexoffice integration for lead {LeadId}.", lead.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Lexoffice contact for lead {LeadId}. Continuing without Lexoffice integration.", lead.Id);
            // Continue without Lexoffice contact - Lexoffice is optional
        }

        var createdLead = await _leadRepository.AddAsync(lead, cancellationToken);
        return MapToDto(createdLead, partner);
    }

    public async Task<LeadDto?> GetLeadByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(id, cancellationToken);
        if (lead == null) return null;

        // Check if Lexoffice contact still exists
        if (!string.IsNullOrEmpty(lead.LexofficeContactId))
        {
            try
            {
                var contactInfo = await _lexofficeService.GetContactAsync(lead.LexofficeContactId, cancellationToken);
                if (contactInfo == null)
                {
                    // Contact no longer exists in Lexoffice, clear the reference
                    _logger.LogWarning("Lexoffice contact {ContactId} no longer exists for lead {LeadId}. Clearing reference.", 
                        lead.LexofficeContactId, lead.Id);
                    lead.LexofficeContactId = null;
                    lead.UpdatedAt = DateTime.UtcNow;
                    await _leadRepository.UpdateAsync(lead, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Lexoffice contact {ContactId} for lead {LeadId}", 
                    lead.LexofficeContactId, lead.Id);
                // Don't fail the lead retrieval if Lexoffice check fails
            }
        }

        var partner = lead.PartnerId != null 
            ? await _partnerRepository.GetByPartnerIdAsync(lead.PartnerId, cancellationToken)
            : null;

        return MapToDto(lead, partner);
    }

    public async Task<IEnumerable<LeadDto>> GetAllLeadsAsync(CancellationToken cancellationToken = default)
    {
        var leads = await _leadRepository.GetAllAsync(cancellationToken);
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        var partnerDict = partners.ToDictionary(p => p.PartnerId);

        return leads.Select(lead => 
        {
            Partner? partner = null;
            if (!string.IsNullOrEmpty(lead.PartnerId))
            {
                partnerDict.TryGetValue(lead.PartnerId, out partner);
            }
            return MapToDto(lead, partner);
        });
    }

    public async Task<IEnumerable<LeadDto>> GetLeadsByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        var leads = await _leadRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        var partner = await _partnerRepository.GetByPartnerIdAsync(partnerId, cancellationToken);

        return leads.Select(lead => MapToDto(lead, partner));
    }

    public async Task<LeadDto> UpdateLeadAsync(Guid id, CreateLeadDto dto, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(id, cancellationToken);
        if (lead == null)
        {
            throw new InvalidOperationException($"Lead with ID '{id}' not found.");
        }

        // Verify partner exists if PartnerId is provided
        Partner? partner = null;
        if (!string.IsNullOrEmpty(dto.PartnerId))
        {
            partner = await _partnerRepository.GetByPartnerIdAsync(dto.PartnerId, cancellationToken);
            if (partner == null)
            {
                throw new InvalidOperationException($"Partner with ID '{dto.PartnerId}' not found.");
            }
        }

        lead.FirstName = dto.FirstName;
        lead.LastName = dto.LastName;
        lead.Email = dto.Email;
        lead.Phone = dto.Phone;
        lead.PartnerId = dto.PartnerId;
        lead.UpdatedAt = DateTime.UtcNow;
        
        // Company Information
        lead.CompanyName = dto.CompanyName;
        lead.CompanyTaxNumber = dto.CompanyTaxNumber;
        lead.CompanyVatRegistrationId = dto.CompanyVatRegistrationId;
        lead.CompanyAllowTaxFreeInvoices = dto.CompanyAllowTaxFreeInvoices;
        
        // Addresses
        lead.BillingStreet = dto.BillingStreet;
        lead.BillingZip = dto.BillingZip;
        lead.BillingCity = dto.BillingCity;
        lead.BillingCountryCode = dto.BillingCountryCode ?? "DE";
        lead.BillingSupplement = dto.BillingSupplement;
        
        lead.ShippingStreet = dto.ShippingStreet;
        lead.ShippingZip = dto.ShippingZip;
        lead.ShippingCity = dto.ShippingCity;
        lead.ShippingCountryCode = dto.ShippingCountryCode ?? "DE";
        lead.ShippingSupplement = dto.ShippingSupplement;
        
        // Email and Phone
        lead.EmailBusiness = dto.EmailBusiness ?? dto.Email;
        lead.EmailOffice = dto.EmailOffice;
        lead.EmailPrivate = dto.EmailPrivate;
        lead.EmailOther = dto.EmailOther;
        
        lead.PhoneBusiness = dto.PhoneBusiness ?? dto.Phone;
        lead.PhoneOffice = dto.PhoneOffice;
        lead.PhoneMobile = dto.PhoneMobile;
        lead.PhonePrivate = dto.PhonePrivate;
        lead.PhoneFax = dto.PhoneFax;
        lead.PhoneOther = dto.PhoneOther;
        
        // Requirements/Notes
        lead.Notes = dto.Requirements;
        
        // Salutation
        lead.Salutation = dto.Salutation;

        await _leadRepository.UpdateAsync(lead, cancellationToken);

        // Sync contact with Lexoffice
        try
        {
            var contactData = new ContactData(
                lead.FirstName,
                lead.LastName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.CompanyTaxNumber,
                lead.CompanyVatRegistrationId,
                lead.CompanyAllowTaxFreeInvoices,
                lead.BillingStreet,
                lead.BillingZip,
                lead.BillingCity,
                lead.BillingCountryCode,
                lead.BillingSupplement,
                lead.ShippingStreet,
                lead.ShippingZip,
                lead.ShippingCity,
                lead.ShippingCountryCode,
                lead.ShippingSupplement,
                lead.EmailBusiness,
                lead.EmailOffice,
                lead.EmailPrivate,
                lead.EmailOther,
                lead.PhoneBusiness,
                lead.PhoneOffice,
                lead.PhoneMobile,
                lead.PhonePrivate,
                lead.PhoneFax,
                lead.PhoneOther,
                lead.Salutation,
                lead.PartnerId
            );

            if (!string.IsNullOrEmpty(lead.LexofficeContactId))
            {
                // Update existing contact in Lexoffice
                var updated = await _lexofficeService.UpdateContactAsync(lead.LexofficeContactId, contactData, cancellationToken);
                if (!updated)
                {
                    _logger.LogWarning("Failed to update Lexoffice contact {ContactId} for lead {LeadId}", 
                        lead.LexofficeContactId, lead.Id);
                }
                else
                {
                    _logger.LogInformation("Successfully updated Lexoffice contact {ContactId} for lead {LeadId}", 
                        lead.LexofficeContactId, lead.Id);
                }
            }
            else
            {
                // Create new contact in Lexoffice if it doesn't exist
                var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
                if (!string.IsNullOrEmpty(lexofficeContactId))
                {
                    lead.LexofficeContactId = lexofficeContactId;
                    await _leadRepository.UpdateAsync(lead, cancellationToken);
                    _logger.LogInformation("Created Lexoffice contact {ContactId} for lead {LeadId}", 
                        lexofficeContactId, lead.Id);
                }
                else
                {
                    _logger.LogDebug("Lexoffice is not configured or contact creation returned empty. Skipping Lexoffice integration for lead {LeadId}.", lead.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Lexoffice contact for lead {LeadId}", lead.Id);
            // Don't fail the lead update if Lexoffice sync fails
        }

        return MapToDto(lead, partner);
    }

    public async Task<LeadDto> UpdateLeadStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(id, cancellationToken);
        if (lead == null)
        {
            throw new InvalidOperationException($"Lead with ID '{id}' not found.");
        }

        if (!Enum.TryParse<LeadStatus>(status, out var leadStatus))
        {
            throw new InvalidOperationException($"Invalid status '{status}'.");
        }

        lead.Status = leadStatus;
        lead.UpdatedAt = DateTime.UtcNow;

        await _leadRepository.UpdateAsync(lead, cancellationToken);

        Partner? partner = null;
        if (!string.IsNullOrEmpty(lead.PartnerId))
        {
            partner = await _partnerRepository.GetByPartnerIdAsync(lead.PartnerId, cancellationToken);
        }
        return MapToDto(lead, partner);
    }

    public async Task<bool> DeleteLeadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(id, cancellationToken);
        if (lead == null) return false;

        // Archive contact in Lexoffice if it exists
        if (!string.IsNullOrEmpty(lead.LexofficeContactId))
        {
            try
            {
                var archived = await _lexofficeService.ArchiveContactAsync(lead.LexofficeContactId, cancellationToken);
                if (!archived)
                {
                    _logger.LogWarning("Failed to archive Lexoffice contact {ContactId} for lead {LeadId}", 
                        lead.LexofficeContactId, lead.Id);
                }
                else
                {
                    _logger.LogInformation("Successfully archived Lexoffice contact {ContactId} for lead {LeadId}", 
                        lead.LexofficeContactId, lead.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Lexoffice contact {ContactId} for lead {LeadId}", 
                    lead.LexofficeContactId, lead.Id);
                // Don't fail the lead deletion if Lexoffice archiving fails
            }
        }

        await _leadRepository.DeleteAsync(lead, cancellationToken);
        return true;
    }

    public async Task<LeadDto?> CreateLexofficeContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(id, cancellationToken);
        if (lead == null)
        {
            return null;
        }

        // Check if contact already exists in Lexoffice
        if (!string.IsNullOrEmpty(lead.LexofficeContactId))
        {
            throw new InvalidOperationException($"Lead already has a Lexoffice contact ID: {lead.LexofficeContactId}");
        }

        try
        {
            var contactData = new ContactData(
                lead.FirstName,
                lead.LastName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.CompanyTaxNumber,
                lead.CompanyVatRegistrationId,
                lead.CompanyAllowTaxFreeInvoices,
                lead.BillingStreet,
                lead.BillingZip,
                lead.BillingCity,
                lead.BillingCountryCode ?? "DE",
                lead.BillingSupplement,
                lead.ShippingStreet,
                lead.ShippingZip,
                lead.ShippingCity,
                lead.ShippingCountryCode ?? "DE",
                lead.ShippingSupplement,
                lead.EmailBusiness ?? lead.Email,
                lead.EmailOffice,
                lead.EmailPrivate,
                lead.EmailOther,
                lead.PhoneBusiness ?? lead.Phone,
                lead.PhoneOffice,
                lead.PhoneMobile,
                lead.PhonePrivate,
                lead.PhoneFax,
                lead.PhoneOther,
                lead.Salutation,
                lead.PartnerId
            );

            var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
            if (!string.IsNullOrEmpty(lexofficeContactId))
            {
                lead.LexofficeContactId = lexofficeContactId;
                lead.UpdatedAt = DateTime.UtcNow;
                await _leadRepository.UpdateAsync(lead, cancellationToken);
                _logger.LogInformation("Created Lexoffice contact {ContactId} for lead {LeadId}", 
                    lexofficeContactId, lead.Id);
            }
            else
            {
                throw new InvalidOperationException("Failed to create Lexoffice contact. No contact ID returned.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Lexoffice contact for lead {LeadId}", lead.Id);
            throw;
        }

        Partner? partner = null;
        if (!string.IsNullOrEmpty(lead.PartnerId))
        {
            partner = await _partnerRepository.GetByPartnerIdAsync(lead.PartnerId, cancellationToken);
        }

        return MapToDto(lead, partner);
    }

    public async Task<LeadDto> ProcessWebhookLeadAsync(WebhookLeadDto dto, CancellationToken cancellationToken = default)
    {
        var createDto = new CreateLeadDto
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            PartnerId = dto.PartnerId,
            PrivacyPolicyAccepted = dto.PrivacyPolicyAccepted,
            TermsAccepted = dto.PrivacyPolicyAccepted, // Assuming same acceptance for webhook
            DataProcessingAccepted = dto.PrivacyPolicyAccepted
        };

        return await CreateLeadAsync(createDto, cancellationToken);
    }

    private static LeadDto MapToDto(Lead lead, Partner? partner)
    {
        return new LeadDto
        {
            Id = lead.Id,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            Email = lead.Email,
            Phone = lead.Phone,
            PartnerId = lead.PartnerId,
            PartnerName = partner?.Name,
            CreatedAt = lead.CreatedAt,
            UpdatedAt = lead.UpdatedAt,
            Status = lead.Status.ToString(),
            Notes = lead.Notes,
            PrivacyPolicyAccepted = lead.PrivacyPolicyAccepted,
            PrivacyPolicyAcceptedAt = lead.PrivacyPolicyAcceptedAt,
            TermsAccepted = lead.TermsAccepted,
            TermsAcceptedAt = lead.TermsAcceptedAt,
            DataProcessingAccepted = lead.DataProcessingAccepted,
            DataProcessingAcceptedAt = lead.DataProcessingAcceptedAt,
            LexofficeContactId = lead.LexofficeContactId,
            
            // Company Information
            CompanyName = lead.CompanyName,
            CompanyTaxNumber = lead.CompanyTaxNumber,
            CompanyVatRegistrationId = lead.CompanyVatRegistrationId,
            CompanyAllowTaxFreeInvoices = lead.CompanyAllowTaxFreeInvoices,
            
            // Billing Address
            BillingStreet = lead.BillingStreet,
            BillingZip = lead.BillingZip,
            BillingCity = lead.BillingCity,
            BillingCountryCode = lead.BillingCountryCode,
            BillingSupplement = lead.BillingSupplement,
            
            // Shipping Address
            ShippingStreet = lead.ShippingStreet,
            ShippingZip = lead.ShippingZip,
            ShippingCity = lead.ShippingCity,
            ShippingCountryCode = lead.ShippingCountryCode,
            ShippingSupplement = lead.ShippingSupplement,
            
            // Email Addresses
            EmailBusiness = lead.EmailBusiness,
            EmailOffice = lead.EmailOffice,
            EmailPrivate = lead.EmailPrivate,
            EmailOther = lead.EmailOther,
            
            // Phone Numbers
            PhoneBusiness = lead.PhoneBusiness,
            PhoneOffice = lead.PhoneOffice,
            PhoneMobile = lead.PhoneMobile,
            PhonePrivate = lead.PhonePrivate,
            PhoneFax = lead.PhoneFax,
            PhoneOther = lead.PhoneOther,
            
            // Salutation
            Salutation = lead.Salutation
        };
    }
}

