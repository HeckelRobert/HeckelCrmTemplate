using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Entities;
using HeckelCrm.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeckelCrm.Core.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly ILexofficeService _lexofficeService;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository contactRepository,
        IPartnerRepository partnerRepository,
        ILexofficeService lexofficeService,
        ILogger<ContactService> logger)
    {
        _contactRepository = contactRepository;
        _partnerRepository = partnerRepository;
        _lexofficeService = lexofficeService;
        _logger = logger;
    }

    public async Task<ContactDto> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default)
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

        // Check if contact already exists - if so, update it instead of creating a new one
        var existingContact = await _contactRepository.GetByEmailAsync(dto.Email, cancellationToken);
        if (existingContact != null)
        {
            _logger.LogInformation("Contact with email '{Email}' already exists. Updating existing contact {ContactId}.", dto.Email, existingContact.Id);
            return await UpdateContactAsync(existingContact.Id, dto, cancellationToken);
        }

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            PartnerId = dto.PartnerId,
            CreatedAt = DateTime.UtcNow,
            BillingStatus = BillingStatus.New,
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
                dto.PartnerId
            );
            var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
            if (!string.IsNullOrEmpty(lexofficeContactId))
            {
                contact.LexofficeContactId = lexofficeContactId;
                _logger.LogInformation("Created Lexoffice contact for contact {ContactId}: {LexofficeContactId}", contact.Id, lexofficeContactId);
            }
            else
            {
                _logger.LogDebug("Lexoffice is not configured or contact creation returned empty. Skipping Lexoffice integration for contact {ContactId}.", contact.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Lexoffice contact for contact {ContactId}. Continuing without Lexoffice integration.", contact.Id);
        }

        var createdContact = await _contactRepository.AddAsync(contact, cancellationToken);
        return MapToDto(createdContact, partner);
    }

    public async Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact == null) return null;

        // Check if Lexoffice contact still exists
        if (!string.IsNullOrEmpty(contact.LexofficeContactId))
        {
            try
            {
                var contactInfo = await _lexofficeService.GetContactAsync(contact.LexofficeContactId, cancellationToken);
                if (contactInfo == null)
                {
                    _logger.LogWarning("Lexoffice contact {ContactId} no longer exists for contact {ContactId}. Clearing reference.", 
                        contact.LexofficeContactId, contact.Id);
                    contact.LexofficeContactId = null;
                    contact.UpdatedAt = DateTime.UtcNow;
                    await _contactRepository.UpdateAsync(contact, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Lexoffice contact {ContactId} for contact {ContactId}", 
                    contact.LexofficeContactId, contact.Id);
            }
        }

        var partner = contact.PartnerId != null 
            ? await _partnerRepository.GetByPartnerIdAsync(contact.PartnerId, cancellationToken)
            : null;

        return MapToDto(contact, partner);
    }

    public async Task<IEnumerable<ContactDto>> GetAllContactsAsync(CancellationToken cancellationToken = default)
    {
        var contacts = await _contactRepository.GetAllAsync(cancellationToken);
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        var partnerDict = partners.ToDictionary(p => p.PartnerId);

        return contacts.Select(contact => 
        {
            Partner? partner = null;
            if (!string.IsNullOrEmpty(contact.PartnerId))
            {
                partnerDict.TryGetValue(contact.PartnerId, out partner);
            }
            return MapToDto(contact, partner);
        });
    }

    public async Task<IEnumerable<ContactDto>> GetContactsByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        var contacts = await _contactRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        var partner = await _partnerRepository.GetByPartnerIdAsync(partnerId, cancellationToken);

        return contacts.Select(contact => MapToDto(contact, partner));
    }

    public async Task<ContactDto> UpdateContactAsync(Guid id, CreateContactDto dto, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            throw new InvalidOperationException($"Contact with ID '{id}' not found.");
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

        contact.FirstName = dto.FirstName;
        contact.LastName = dto.LastName;
        contact.Email = dto.Email;
        contact.Phone = dto.Phone;
        contact.PartnerId = dto.PartnerId;
        contact.UpdatedAt = DateTime.UtcNow;
        
        // Company Information
        contact.CompanyName = dto.CompanyName;
        contact.CompanyTaxNumber = dto.CompanyTaxNumber;
        contact.CompanyVatRegistrationId = dto.CompanyVatRegistrationId;
        contact.CompanyAllowTaxFreeInvoices = dto.CompanyAllowTaxFreeInvoices;
        
        // Addresses
        contact.BillingStreet = dto.BillingStreet;
        contact.BillingZip = dto.BillingZip;
        contact.BillingCity = dto.BillingCity;
        contact.BillingCountryCode = dto.BillingCountryCode ?? "DE";
        contact.BillingSupplement = dto.BillingSupplement;
        
        contact.ShippingStreet = dto.ShippingStreet;
        contact.ShippingZip = dto.ShippingZip;
        contact.ShippingCity = dto.ShippingCity;
        contact.ShippingCountryCode = dto.ShippingCountryCode ?? "DE";
        contact.ShippingSupplement = dto.ShippingSupplement;
        
        // Email and Phone
        contact.EmailBusiness = dto.EmailBusiness ?? dto.Email;
        contact.EmailOffice = dto.EmailOffice;
        contact.EmailPrivate = dto.EmailPrivate;
        contact.EmailOther = dto.EmailOther;
        
        contact.PhoneBusiness = dto.PhoneBusiness ?? dto.Phone;
        contact.PhoneOffice = dto.PhoneOffice;
        contact.PhoneMobile = dto.PhoneMobile;
        contact.PhonePrivate = dto.PhonePrivate;
        contact.PhoneFax = dto.PhoneFax;
        contact.PhoneOther = dto.PhoneOther;
        
        // Requirements/Notes
        contact.Notes = dto.Requirements;
        
        // Salutation
        contact.Salutation = dto.Salutation;

        await _contactRepository.UpdateAsync(contact, cancellationToken);

        // Sync contact with Lexoffice
        try
        {
            var contactData = new ContactData(
                contact.FirstName,
                contact.LastName,
                contact.Email,
                contact.Phone,
                contact.CompanyName,
                contact.CompanyTaxNumber,
                contact.CompanyVatRegistrationId,
                contact.CompanyAllowTaxFreeInvoices,
                contact.BillingStreet,
                contact.BillingZip,
                contact.BillingCity,
                contact.BillingCountryCode ?? "DE",
                contact.BillingSupplement,
                contact.ShippingStreet,
                contact.ShippingZip,
                contact.ShippingCity,
                contact.ShippingCountryCode ?? "DE",
                contact.ShippingSupplement,
                contact.EmailBusiness ?? contact.Email,
                contact.EmailOffice,
                contact.EmailPrivate,
                contact.EmailOther,
                contact.PhoneBusiness ?? contact.Phone,
                contact.PhoneOffice,
                contact.PhoneMobile,
                contact.PhonePrivate,
                contact.PhoneFax,
                contact.PhoneOther,
                contact.Salutation,
                contact.PartnerId
            );

            if (!string.IsNullOrEmpty(contact.LexofficeContactId))
            {
                var updated = await _lexofficeService.UpdateContactAsync(contact.LexofficeContactId, contactData, cancellationToken);
                if (!updated)
                {
                    _logger.LogWarning("Failed to update Lexoffice contact {ContactId} for contact {ContactId}", 
                        contact.LexofficeContactId, contact.Id);
                }
            }
            else
            {
                var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
                if (!string.IsNullOrEmpty(lexofficeContactId))
                {
                    contact.LexofficeContactId = lexofficeContactId;
                    await _contactRepository.UpdateAsync(contact, cancellationToken);
                    _logger.LogInformation("Created Lexoffice contact {ContactId} for contact {ContactId}", 
                        lexofficeContactId, contact.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Lexoffice contact for contact {ContactId}", contact.Id);
        }

        return MapToDto(contact, partner);
    }

    public async Task<ContactDto> UpdateBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            throw new InvalidOperationException($"Contact with ID '{id}' not found.");
        }

        if (!Enum.TryParse<BillingStatus>(dto.BillingStatus, out var newStatus))
        {
            throw new InvalidOperationException($"Invalid billing status '{dto.BillingStatus}'.");
        }

        var currentStatus = contact.BillingStatus;

        // Validate status transitions based on permissions
        if (!CanChangeBillingStatus(currentStatus, newStatus, isAdmin))
        {
            throw new InvalidOperationException(
                $"Cannot change billing status from {currentStatus} to {newStatus}. " +
                $"{(isAdmin ? "Admin" : "Partner")} permissions do not allow this transition.");
        }

        contact.BillingStatus = newStatus;
        contact.UpdatedAt = DateTime.UtcNow;

        await _contactRepository.UpdateAsync(contact, cancellationToken);

        var partner = contact.PartnerId != null 
            ? await _partnerRepository.GetByPartnerIdAsync(contact.PartnerId, cancellationToken)
            : null;

        return MapToDto(contact, partner);
    }

    private static bool CanChangeBillingStatus(BillingStatus currentStatus, BillingStatus newStatus, bool isAdmin)
    {
        // Status cannot be changed to the same status
        if (currentStatus == newStatus)
        {
            return false;
        }

        // Once paid, cannot go back to any other status
        if (currentStatus == BillingStatus.Paid)
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
            return currentStatus == BillingStatus.New && newStatus == BillingStatus.Billed;
        }

        // Admin can change: New -> Billed, Billed -> Paid
        return (currentStatus == BillingStatus.New && newStatus == BillingStatus.Billed) ||
               (currentStatus == BillingStatus.Billed && newStatus == BillingStatus.Paid);
    }

    public async Task<bool> DeleteContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact == null) return false;

        // Archive contact in Lexoffice if it exists
        if (!string.IsNullOrEmpty(contact.LexofficeContactId))
        {
            try
            {
                var archived = await _lexofficeService.ArchiveContactAsync(contact.LexofficeContactId, cancellationToken);
                if (!archived)
                {
                    _logger.LogWarning("Failed to archive Lexoffice contact {ContactId} for contact {ContactId}", 
                        contact.LexofficeContactId, contact.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Lexoffice contact {ContactId} for contact {ContactId}", 
                    contact.LexofficeContactId, contact.Id);
            }
        }

        await _contactRepository.DeleteAsync(contact, cancellationToken);
        return true;
    }

    public async Task<ContactDto?> CreateLexofficeContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await _contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(contact.LexofficeContactId))
        {
            throw new InvalidOperationException($"Contact already has a Lexoffice contact ID: {contact.LexofficeContactId}");
        }

        try
        {
            var contactData = new ContactData(
                contact.FirstName,
                contact.LastName,
                contact.Email,
                contact.Phone,
                contact.CompanyName,
                contact.CompanyTaxNumber,
                contact.CompanyVatRegistrationId,
                contact.CompanyAllowTaxFreeInvoices,
                contact.BillingStreet,
                contact.BillingZip,
                contact.BillingCity,
                contact.BillingCountryCode ?? "DE",
                contact.BillingSupplement,
                contact.ShippingStreet,
                contact.ShippingZip,
                contact.ShippingCity,
                contact.ShippingCountryCode ?? "DE",
                contact.ShippingSupplement,
                contact.EmailBusiness ?? contact.Email,
                contact.EmailOffice,
                contact.EmailPrivate,
                contact.EmailOther,
                contact.PhoneBusiness ?? contact.Phone,
                contact.PhoneOffice,
                contact.PhoneMobile,
                contact.PhonePrivate,
                contact.PhoneFax,
                contact.PhoneOther,
                contact.Salutation,
                contact.PartnerId
            );

            var lexofficeContactId = await _lexofficeService.CreateContactAsync(contactData, cancellationToken);
            if (!string.IsNullOrEmpty(lexofficeContactId))
            {
                contact.LexofficeContactId = lexofficeContactId;
                contact.UpdatedAt = DateTime.UtcNow;
                await _contactRepository.UpdateAsync(contact, cancellationToken);
                _logger.LogInformation("Created Lexoffice contact {ContactId} for contact {ContactId}", 
                    lexofficeContactId, contact.Id);
            }
            else
            {
                throw new InvalidOperationException("Failed to create Lexoffice contact. No contact ID returned.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Lexoffice contact for contact {ContactId}", contact.Id);
            throw;
        }

        Partner? partner = null;
        if (!string.IsNullOrEmpty(contact.PartnerId))
        {
            partner = await _partnerRepository.GetByPartnerIdAsync(contact.PartnerId, cancellationToken);
        }

        return MapToDto(contact, partner);
    }

    public async Task<ContactDto> ProcessWebhookContactAsync(WebhookContactDto dto, CancellationToken cancellationToken = default)
    {
        var createDto = new CreateContactDto
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.Company,
            PartnerId = dto.PartnerId,
            PrivacyPolicyAccepted = dto.PrivacyPolicyAccepted,
            TermsAccepted = dto.PrivacyPolicyAccepted,
            DataProcessingAccepted = dto.PrivacyPolicyAccepted
        };

        return await CreateContactAsync(createDto, cancellationToken);
    }

    private static ContactDto MapToDto(Contact contact, Partner? partner)
    {
        return new ContactDto
        {
            Id = contact.Id,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            Phone = contact.Phone,
            PartnerId = contact.PartnerId,
            PartnerName = partner?.Name,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
            BillingStatus = contact.BillingStatus.ToString(),
            Notes = contact.Notes,
            PrivacyPolicyAccepted = contact.PrivacyPolicyAccepted,
            PrivacyPolicyAcceptedAt = contact.PrivacyPolicyAcceptedAt,
            TermsAccepted = contact.TermsAccepted,
            TermsAcceptedAt = contact.TermsAcceptedAt,
            DataProcessingAccepted = contact.DataProcessingAccepted,
            DataProcessingAcceptedAt = contact.DataProcessingAcceptedAt,
            LexofficeContactId = contact.LexofficeContactId,
            
            // Company Information
            CompanyName = contact.CompanyName,
            CompanyTaxNumber = contact.CompanyTaxNumber,
            CompanyVatRegistrationId = contact.CompanyVatRegistrationId,
            CompanyAllowTaxFreeInvoices = contact.CompanyAllowTaxFreeInvoices,
            
            // Billing Address
            BillingStreet = contact.BillingStreet,
            BillingZip = contact.BillingZip,
            BillingCity = contact.BillingCity,
            BillingCountryCode = contact.BillingCountryCode,
            BillingSupplement = contact.BillingSupplement,
            
            // Shipping Address
            ShippingStreet = contact.ShippingStreet,
            ShippingZip = contact.ShippingZip,
            ShippingCity = contact.ShippingCity,
            ShippingCountryCode = contact.ShippingCountryCode,
            ShippingSupplement = contact.ShippingSupplement,
            
            // Email Addresses
            EmailBusiness = contact.EmailBusiness,
            EmailOffice = contact.EmailOffice,
            EmailPrivate = contact.EmailPrivate,
            EmailOther = contact.EmailOther,
            
            // Phone Numbers
            PhoneBusiness = contact.PhoneBusiness,
            PhoneOffice = contact.PhoneOffice,
            PhoneMobile = contact.PhoneMobile,
            PhonePrivate = contact.PhonePrivate,
            PhoneFax = contact.PhoneFax,
            PhoneOther = contact.PhoneOther,
            
            // Salutation
            Salutation = contact.Salutation
        };
    }
}

