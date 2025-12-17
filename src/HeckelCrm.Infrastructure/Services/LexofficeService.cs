using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeckelCrm.Infrastructure.Services;

public class LexofficeService : ILexofficeService
{
    private readonly HttpClient _httpClient;
    private readonly LexofficeOptions _options;
    private readonly ILogger<LexofficeService> _logger;
    private readonly IAdminSettingsService _adminSettingsService;
    private string? _apiKey;

    // Internal models for Lexoffice quotation request (property names match API: lower case)
    private sealed class LexofficeUnitPrice
    {
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "EUR";

        [JsonPropertyName("netAmount")]
        public decimal NetAmount { get; set; }

        [JsonPropertyName("taxRatePercentage")]
        public int TaxRatePercentage { get; set; }
    }

    private sealed class LexofficeLineItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "service";

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("unitName")]
        public string UnitName { get; set; } = string.Empty;

        [JsonPropertyName("unitPrice")]
        public LexofficeUnitPrice UnitPrice { get; set; } = new();
    }

    public LexofficeService(
        HttpClient httpClient,
        IOptions<LexofficeOptions> options,
        ILogger<LexofficeService> logger,
        IAdminSettingsService adminSettingsService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _adminSettingsService = adminSettingsService;
        
        // Initialize API key from admin settings or fallback to options
        InitializeApiKeyAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeApiKeyAsync()
    {
        try
        {
            var adminSettings = await _adminSettingsService.GetSettingsAsync();
            _apiKey = adminSettings.LexofficeApiKey;
            
            // Fallback to configuration if not set in admin settings
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = _options.ApiKey ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Lexoffice API key from admin settings, falling back to configuration");
            _apiKey = _options.ApiKey ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(_apiKey))
        {
            // Always ensure BaseAddress is set correctly
            var baseUrl = _options.BaseUrl ?? "https://api.lexware.io/v1";
            baseUrl = baseUrl.TrimEnd('/');
            _httpClient.BaseAddress = new Uri(baseUrl);
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");            
            // Content-Type is automatically set by StringContent
        }
        else
        {
            _logger.LogWarning("Lexoffice API key is not configured. Lexoffice integration will be disabled.");
        }
    }

    private async Task RefreshApiKeyAsync()
    {
        try
        {
            var adminSettings = await _adminSettingsService.GetSettingsAsync();
            var newApiKey = adminSettings.LexofficeApiKey ?? _options.ApiKey ?? string.Empty;
            
            if (newApiKey != _apiKey)
            {
                _apiKey = newApiKey;
                
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                    _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Lexoffice API key from admin settings");
        }
    }

    private async Task<bool> IsConfiguredAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            await RefreshApiKeyAsync();
        }
        return !string.IsNullOrEmpty(_apiKey);
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public string GetApiKey() => _apiKey ?? string.Empty;


    public async Task<string> CreateContactAsync(ContactData contact, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Skipping contact creation.");
            return string.Empty;
        }

        var request = new LexofficeContactRequest
        {
            Version = 0,
            Roles = new LexofficeContactRoles
            {
                Customer = new LexofficeCustomerRole()
            }
        };

        // Build person or company (but not both)
        // According to the working Postman test, company is used when CompanyName is provided
        if (!string.IsNullOrEmpty(contact.CompanyName))
        {
            // Company contact - only include fields that are actually set
            var company = new LexofficeCompany
            {
                Name = contact.CompanyName
            };

            // Only add optional company fields if they are provided
            if (!string.IsNullOrEmpty(contact.CompanyTaxNumber))
            {
                company.TaxNumber = contact.CompanyTaxNumber;
            }
            if (!string.IsNullOrEmpty(contact.CompanyVatRegistrationId))
            {
                company.VatRegistrationId = contact.CompanyVatRegistrationId;
            }
            if (contact.CompanyAllowTaxFreeInvoices)
            {
                company.AllowTaxFreeInvoices = contact.CompanyAllowTaxFreeInvoices;
            }

            // Add contact person if name is provided (as per working Postman example)
            if (!string.IsNullOrEmpty(contact.FirstName) || !string.IsNullOrEmpty(contact.LastName))
            {
                var contactPerson = new LexofficeContactPerson
                {
                    FirstName = contact.FirstName ?? string.Empty,
                    LastName = contact.LastName ?? string.Empty,
                    Primary = false, // As per Postman example
                    EmailAddress = contact.Email,
                    PhoneNumber = contact.Phone ?? contact.PhoneBusiness ?? contact.PhoneMobile
                };

                if (!string.IsNullOrEmpty(contact.Salutation))
                {
                    contactPerson.Salutation = contact.Salutation;
                }

                company.ContactPersons = new List<LexofficeContactPerson> { contactPerson };
            }

            request.Company = company;
        }
        else
        {
            // Person contact
            request.Person = new LexofficePerson
            {
                FirstName = contact.FirstName,
                LastName = contact.LastName
            };

            if (!string.IsNullOrEmpty(contact.Salutation))
            {
                request.Person.Salutation = contact.Salutation;
            }
        }

        // Build addresses
        var addresses = new LexofficeAddresses();
        bool hasAddresses = false;

        if (!string.IsNullOrEmpty(contact.BillingStreet) && !string.IsNullOrEmpty(contact.BillingCity))
        {
            addresses.Billing = new List<LexofficeAddress>
            {
                new LexofficeAddress
                {
                    Street = contact.BillingStreet,
                    Zip = contact.BillingZip,
                    City = contact.BillingCity,
                    CountryCode = contact.BillingCountryCode ?? "DE",
                    Supplement = contact.BillingSupplement
                }
            };
            hasAddresses = true;
        }

        if (!string.IsNullOrEmpty(contact.ShippingStreet) && !string.IsNullOrEmpty(contact.ShippingCity))
        {
            addresses.Shipping = new List<LexofficeAddress>
            {
                new LexofficeAddress
                {
                    Street = contact.ShippingStreet,
                    Zip = contact.ShippingZip,
                    City = contact.ShippingCity,
                    CountryCode = contact.ShippingCountryCode ?? "DE",
                    Supplement = contact.ShippingSupplement
                }
            };
            hasAddresses = true;
        }

        if (hasAddresses)
        {
            request.Addresses = addresses;
        }

        // Build email addresses
        var emailAddresses = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(contact.EmailBusiness)) emailAddresses["business"] = new List<string> { contact.EmailBusiness };
        if (!string.IsNullOrEmpty(contact.EmailOffice)) emailAddresses["office"] = new List<string> { contact.EmailOffice };
        if (!string.IsNullOrEmpty(contact.EmailPrivate)) emailAddresses["private"] = new List<string> { contact.EmailPrivate };
        if (!string.IsNullOrEmpty(contact.EmailOther)) emailAddresses["other"] = new List<string> { contact.EmailOther };
        // Fallback to main email if no specific email provided
        if (!emailAddresses.Any() && !string.IsNullOrEmpty(contact.Email))
        {
            emailAddresses["business"] = new List<string> { contact.Email };
        }

        if (emailAddresses.Any())
        {
            request.EmailAddresses = emailAddresses;
        }

        // Build phone numbers
        var phoneNumbers = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(contact.PhoneBusiness)) phoneNumbers["business"] = new List<string> { contact.PhoneBusiness };
        if (!string.IsNullOrEmpty(contact.PhoneOffice)) phoneNumbers["office"] = new List<string> { contact.PhoneOffice };
        if (!string.IsNullOrEmpty(contact.PhoneMobile)) phoneNumbers["mobile"] = new List<string> { contact.PhoneMobile };
        if (!string.IsNullOrEmpty(contact.PhonePrivate)) phoneNumbers["private"] = new List<string> { contact.PhonePrivate };
        if (!string.IsNullOrEmpty(contact.PhoneFax)) phoneNumbers["fax"] = new List<string> { contact.PhoneFax };
        if (!string.IsNullOrEmpty(contact.PhoneOther)) phoneNumbers["other"] = new List<string> { contact.PhoneOther };
        // Fallback to main phone if no specific phone provided
        if (!phoneNumbers.Any() && !string.IsNullOrEmpty(contact.Phone))
        {
            phoneNumbers["business"] = new List<string> { contact.Phone };
        }

        if (phoneNumbers.Any())
        {
            request.PhoneNumbers = phoneNumbers;
        }
       
        // Add PartnerId to note field if available
        if (!string.IsNullOrEmpty(contact.PartnerId))
        {
            request.Note = contact.PartnerId;
        }

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        });

        // Ensure BaseAddress is set correctly - use absolute URI
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl ?? "https://api.lexware.io/v1";
        baseUrl = baseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/contacts";
        
        _logger.LogInformation("Creating Lexoffice contact. BaseAddress: {BaseAddress}, Full URL: {Url}, Payload: {Payload}", 
            _httpClient.BaseAddress, fullUrl, json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        // Use absolute URI to ensure correct endpoint
        var requestUri = new Uri(fullUrl);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        _logger.LogInformation("Lexoffice API response. Status: {Status}, Request URL: {Url}", 
            response.StatusCode, response.RequestMessage?.RequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Lexoffice contact. URL: {Url}, Status: {Status}, Error: {Error}", 
                fullUrl, response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to create Lexoffice contact: {response.StatusCode}. Error: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var contactResponse = JsonSerializer.Deserialize<LexofficeContactResponse>(responseContent);
        
        if (contactResponse == null || string.IsNullOrEmpty(contactResponse.Id))
        {
            throw new InvalidOperationException("Contact ID not found in response");
        }

        return contactResponse.Id;
    }

    public async Task<bool> UpdateContactAsync(string contactId, ContactData contact, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Skipping contact update.");
            return false;
        }

        // First, retrieve the existing contact to get the version for optimistic locking
        var existingContactResponse = await _httpClient.GetAsync($"/contacts/{contactId}", cancellationToken);
        if (!existingContactResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to retrieve existing contact {ContactId} for update. Status: {Status}", 
                contactId, existingContactResponse.StatusCode);
            return false;
        }

        var existingContactContent = await existingContactResponse.Content.ReadAsStringAsync(cancellationToken);
        var existingContact = JsonSerializer.Deserialize<LexofficeContactResponse>(existingContactContent);
        
        if (existingContact == null)
        {
            _logger.LogError("Failed to deserialize existing contact {ContactId}", contactId);
            return false;
        }

        // Build the update request using the same logic as CreateContactAsync
        var request = new LexofficeContactRequest
        {
            Version = existingContact.Version, // Use existing version for optimistic locking
            Roles = new LexofficeContactRoles
            {
                Customer = new LexofficeCustomerRole()
            }
        };

        // Build person or company (but not both)
        if (!string.IsNullOrEmpty(contact.CompanyName))
        {
            var company = new LexofficeCompany
            {
                Name = contact.CompanyName
            };

            if (!string.IsNullOrEmpty(contact.CompanyTaxNumber))
            {
                company.TaxNumber = contact.CompanyTaxNumber;
            }
            if (!string.IsNullOrEmpty(contact.CompanyVatRegistrationId))
            {
                company.VatRegistrationId = contact.CompanyVatRegistrationId;
            }
            if (contact.CompanyAllowTaxFreeInvoices)
            {
                company.AllowTaxFreeInvoices = contact.CompanyAllowTaxFreeInvoices;
            }

            if (!string.IsNullOrEmpty(contact.FirstName) || !string.IsNullOrEmpty(contact.LastName))
            {
                var contactPerson = new LexofficeContactPerson
                {
                    FirstName = contact.FirstName ?? string.Empty,
                    LastName = contact.LastName ?? string.Empty,
                    Primary = false,
                    EmailAddress = contact.Email,
                    PhoneNumber = contact.Phone ?? contact.PhoneBusiness ?? contact.PhoneMobile
                };

                if (!string.IsNullOrEmpty(contact.Salutation))
                {
                    contactPerson.Salutation = contact.Salutation;
                }

                company.ContactPersons = new List<LexofficeContactPerson> { contactPerson };
            }

            request.Company = company;
        }
        else
        {
            request.Person = new LexofficePerson
            {
                FirstName = contact.FirstName,
                LastName = contact.LastName
            };

            if (!string.IsNullOrEmpty(contact.Salutation))
            {
                request.Person.Salutation = contact.Salutation;
            }
        }

        // Build addresses
        var addresses = new LexofficeAddresses();
        bool hasAddresses = false;

        if (!string.IsNullOrEmpty(contact.BillingStreet) && !string.IsNullOrEmpty(contact.BillingCity))
        {
            addresses.Billing = new List<LexofficeAddress>
            {
                new LexofficeAddress
                {
                    Street = contact.BillingStreet,
                    Zip = contact.BillingZip,
                    City = contact.BillingCity,
                    CountryCode = contact.BillingCountryCode ?? "DE",
                    Supplement = contact.BillingSupplement
                }
            };
            hasAddresses = true;
        }

        if (!string.IsNullOrEmpty(contact.ShippingStreet) && !string.IsNullOrEmpty(contact.ShippingCity))
        {
            addresses.Shipping = new List<LexofficeAddress>
            {
                new LexofficeAddress
                {
                    Street = contact.ShippingStreet,
                    Zip = contact.ShippingZip,
                    City = contact.ShippingCity,
                    CountryCode = contact.ShippingCountryCode ?? "DE",
                    Supplement = contact.ShippingSupplement
                }
            };
            hasAddresses = true;
        }

        if (hasAddresses)
        {
            request.Addresses = addresses;
        }

        // Build email addresses
        var emailAddresses = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(contact.EmailBusiness)) emailAddresses["business"] = new List<string> { contact.EmailBusiness };
        if (!string.IsNullOrEmpty(contact.EmailOffice)) emailAddresses["office"] = new List<string> { contact.EmailOffice };
        if (!string.IsNullOrEmpty(contact.EmailPrivate)) emailAddresses["private"] = new List<string> { contact.EmailPrivate };
        if (!string.IsNullOrEmpty(contact.EmailOther)) emailAddresses["other"] = new List<string> { contact.EmailOther };
        if (!emailAddresses.Any() && !string.IsNullOrEmpty(contact.Email))
        {
            emailAddresses["business"] = new List<string> { contact.Email };
        }

        if (emailAddresses.Any())
        {
            request.EmailAddresses = emailAddresses;
        }

        // Build phone numbers
        var phoneNumbers = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(contact.PhoneBusiness)) phoneNumbers["business"] = new List<string> { contact.PhoneBusiness };
        if (!string.IsNullOrEmpty(contact.PhoneOffice)) phoneNumbers["office"] = new List<string> { contact.PhoneOffice };
        if (!string.IsNullOrEmpty(contact.PhoneMobile)) phoneNumbers["mobile"] = new List<string> { contact.PhoneMobile };
        if (!string.IsNullOrEmpty(contact.PhonePrivate)) phoneNumbers["private"] = new List<string> { contact.PhonePrivate };
        if (!string.IsNullOrEmpty(contact.PhoneFax)) phoneNumbers["fax"] = new List<string> { contact.PhoneFax };
        if (!string.IsNullOrEmpty(contact.PhoneOther)) phoneNumbers["other"] = new List<string> { contact.PhoneOther };
        if (!phoneNumbers.Any() && !string.IsNullOrEmpty(contact.Phone))
        {
            phoneNumbers["business"] = new List<string> { contact.Phone };
        }

        if (phoneNumbers.Any())
        {
            request.PhoneNumbers = phoneNumbers;
        }
       
        // Add PartnerId to note field if available
        if (!string.IsNullOrEmpty(contact.PartnerId))
        {
            request.Note = contact.PartnerId;
        }

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        });

        var fullUrl = $"{_httpClient.BaseAddress}/contacts/{contactId}";
        _logger.LogInformation("Updating Lexoffice contact {ContactId}. URL: {Url}, Payload: {Payload}", 
            contactId, fullUrl, json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"/contacts/{contactId}", content, cancellationToken);
        
        _logger.LogInformation("Lexoffice API response. Status: {Status}, Request URL: {Url}", 
            response.StatusCode, response.RequestMessage?.RequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to update Lexoffice contact {ContactId}. URL: {Url}, Status: {Status}, Error: {Error}", 
                contactId, fullUrl, response.StatusCode, errorContent);
            return false;
        }

        return true;
    }

    public async Task<bool> ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Skipping contact archiving.");
            return false;
        }

        // First, retrieve the existing contact to get the version and current data
        var existingContactResponse = await _httpClient.GetAsync($"/contacts/{contactId}", cancellationToken);
        if (!existingContactResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to retrieve existing contact {ContactId} for archiving. Status: {Status}", 
                contactId, existingContactResponse.StatusCode);
            return false;
        }

        var existingContactContent = await existingContactResponse.Content.ReadAsStringAsync(cancellationToken);
        var existingContact = JsonSerializer.Deserialize<LexofficeContactResponse>(existingContactContent);
        
        if (existingContact == null)
        {
            _logger.LogError("Failed to deserialize existing contact {ContactId}", contactId);
            return false;
        }

        // Build the update request with archived = true
        // We need to preserve all existing data and just set archived to true
        var request = new LexofficeContactRequest
        {
            Version = existingContact.Version,
            Roles = existingContact.Roles ?? new LexofficeContactRoles { Customer = new LexofficeCustomerRole() },
            Person = existingContact.Person,
            Company = existingContact.Company,
            Addresses = existingContact.Addresses,
            EmailAddresses = existingContact.EmailAddresses,
            PhoneNumbers = existingContact.PhoneNumbers,
            Note = existingContact.Note,
            Archived = true // Set archived flag
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        });

        var fullUrl = $"{_httpClient.BaseAddress}/contacts/{contactId}";
        _logger.LogInformation("Archiving Lexoffice contact {ContactId}. URL: {Url}", contactId, fullUrl);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"/contacts/{contactId}", content, cancellationToken);
        
        _logger.LogInformation("Lexoffice API response for archiving. Status: {Status}, Request URL: {Url}", 
            response.StatusCode, response.RequestMessage?.RequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to archive Lexoffice contact {ContactId}. URL: {Url}, Status: {Status}, Error: {Error}", 
                contactId, fullUrl, response.StatusCode, errorContent);
            return false;
        }

        return true;
    }

    public async Task<LexofficeContactInfo?> GetContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Cannot retrieve contact.");
            return null;
        }

        var baseUrl = _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl ?? "https://api.lexware.io/v1";
        baseUrl = baseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/contacts/{contactId}";
        
        _logger.LogInformation("Retrieving Lexoffice contact {ContactId}. URL: {Url}", contactId, fullUrl);

        var requestUri = new Uri(fullUrl);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        _logger.LogInformation("Lexoffice API response for contact retrieval. Status: {Status}, Request URL: {Url}", 
            response.StatusCode, response.RequestMessage?.RequestUri);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Lexoffice contact {ContactId} not found (404).", contactId);
                return null;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to retrieve Lexoffice contact {ContactId}. URL: {Url}, Status: {Status}, Error: {Error}", 
                contactId, fullUrl, response.StatusCode, errorContent);
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var contactResponse = JsonSerializer.Deserialize<LexofficeContactResponse>(responseContent);
        
        if (contactResponse == null)
        {
            _logger.LogError("Failed to deserialize Lexoffice contact {ContactId}", contactId);
            return null;
        }

        return new LexofficeContactInfo(contactResponse.Id, contactResponse.Archived);
    }

    public async Task<string> CreateQuoteAsync(QuoteData quote, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Skipping quote creation.");
            return string.Empty;
        }

        if (quote.LineItems == null || quote.LineItems.Count == 0)
        {
            throw new InvalidOperationException("At least one line item is required to create a quote.");
        }

        // Format voucherDate as ISO 8601 with timezone offset
        var voucherDateString = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+01:00";

        // Format expirationDate from ValidUntil
        var expirationDateString = quote.ValidUntil.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+01:00";

        // Build strongly typed line items (no anonymous types)
        var lineItems = quote.LineItems.Select(item => new LexofficeLineItem
        {
            Id = item.ArticleId,
            Type = item.Type,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitName = item.UnitName,
            UnitPrice = new LexofficeUnitPrice
            {
                Currency = quote.Currency,
                NetAmount = item.UnitPrice,
                TaxRatePercentage = item.TaxRatePercentage
            }
        }).ToList();

        var payload = new
        {
            voucherDate = voucherDateString,
            expirationDate = expirationDateString,
            address = new
            {
                contactId = quote.ContactId
            },
            lineItems,
            totalPrice = new
            {
                currency = quote.Currency
            },
            taxConditions = new
            {
                taxType = "net"
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Build absolute URL to avoid BaseAddress path issues (e.g. missing /v1)
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl ?? "https://api.lexware.io/v1";
        baseUrl = baseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/quotations";

        _logger.LogInformation("Creating Lexoffice quote. URL: {Url}, Payload: {Payload}", fullUrl, json);

        var requestUri = new Uri(fullUrl);
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Lexoffice quote. Status: {Status}, Error: {Error}, Payload: {Payload}", 
                response.StatusCode, errorContent, json);
            throw new HttpRequestException($"Failed to create Lexoffice quote: {response.StatusCode}. Error: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        return result.GetProperty("id").GetString() ?? throw new InvalidOperationException("Quote ID not found in response");
    }

    public async Task<QuoteInfo?> GetQuoteAsync(string quoteId, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Cannot retrieve quote.");
            return null;
        }

        // Build absolute URL to avoid BaseAddress path issues (e.g. missing /v1)
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl ?? "https://api.lexware.io/v1";
        baseUrl = baseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/quotations/{quoteId}";

        _logger.LogInformation("Retrieving Lexoffice quote {QuoteId}. URL: {Url}", quoteId, fullUrl);

        var response = await _httpClient.GetAsync(fullUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get Lexoffice quote. Status: {Status}, Error: {Error}", 
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to get Lexoffice quote: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

        // Extract public link, handling both array and object shapes for "files"
        string? link = null;
        if (result.TryGetProperty("files", out var filesElement))
        {
            if (filesElement.ValueKind == JsonValueKind.Array && filesElement.GetArrayLength() > 0)
            {
                var firstFile = filesElement[0];
                if (firstFile.ValueKind == JsonValueKind.Object &&
                    firstFile.TryGetProperty("href", out var hrefProp) &&
                    hrefProp.ValueKind == JsonValueKind.String)
                {
                    link = hrefProp.GetString();
                }
            }
            else if (filesElement.ValueKind == JsonValueKind.Object &&
                     filesElement.TryGetProperty("href", out var hrefProp2) &&
                     hrefProp2.ValueKind == JsonValueKind.String)
            {
                link = hrefProp2.GetString();
            }
        }

        var quoteNumber = result.TryGetProperty("voucherNumber", out var voucherNumber)
            ? voucherNumber.GetString()
            : null;

        // Extract voucherStatus first (draft, open, accepted, etc.)
        var voucherStatus = result.TryGetProperty("voucherStatus", out var voucherStatusElement)
            ? voucherStatusElement.GetString()
            : null;

        // Prefer voucherStatus for the generic Status field; fall back to archived/active
        string status;
        if (!string.IsNullOrEmpty(voucherStatus))
        {
            status = voucherStatus;
        }
        else
        {
            status = result.TryGetProperty("archived", out var archived) && archived.GetBoolean()
                ? "archived"
                : "active";
        }

        // Prefer explicit expirationDate if available; fall back to old shippingConditions.shippingEndDate
        DateTime? validUntil = null;
        if (result.TryGetProperty("expirationDate", out var expirationDateElement) &&
            expirationDateElement.ValueKind == JsonValueKind.String)
        {
            var expirationDateString = expirationDateElement.GetString();
            if (!string.IsNullOrWhiteSpace(expirationDateString) &&
                DateTime.TryParse(expirationDateString, out var expiration))
            {
                validUntil = expiration;
            }
        }
        else if (result.TryGetProperty("shippingConditions", out var shippingConditions) &&
                 shippingConditions.TryGetProperty("shippingEndDate", out var endDate) &&
                 endDate.ValueKind == JsonValueKind.String)
        {
            var endDateString = endDate.GetString();
            if (!string.IsNullOrWhiteSpace(endDateString) &&
                DateTime.TryParse(endDateString, out var end))
            {
                validUntil = end;
            }
        }

        var createdAt = result.TryGetProperty("voucherDate", out var voucherDate)
                        && voucherDate.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(voucherDate.GetString(), out var createdParsed)
            ? createdParsed
            : DateTime.UtcNow;

        // Extract lineItems
        var lineItems = new List<QuoteLineItemInfo>();
        if (result.TryGetProperty("lineItems", out var lineItemsElement) && lineItemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElement in lineItemsElement.EnumerateArray())
            {
                var articleId = itemElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                var name = itemElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var description = itemElement.TryGetProperty("description", out var descElement) ? descElement.GetString() : null;

                decimal quantity = 0;
                if (itemElement.TryGetProperty("quantity", out var qtyElement) && qtyElement.ValueKind == JsonValueKind.Number)
                {
                    // quantity is always a number in Lexoffice; use decimal for days
                    quantity = qtyElement.GetDecimal();
                }

                var unitName = itemElement.TryGetProperty("unitName", out var unitNameElement) ? unitNameElement.GetString() : null;
                
                decimal? unitPrice = null;
                int? taxRatePercentage = null;
                
                if (itemElement.TryGetProperty("unitPrice", out var unitPriceElement))
                {
                    if (unitPriceElement.TryGetProperty("netAmount", out var netAmountElement) &&
                        netAmountElement.ValueKind == JsonValueKind.Number)
                    {
                        unitPrice = netAmountElement.GetDecimal();
                    }
                    if (unitPriceElement.TryGetProperty("taxRatePercentage", out var taxRateElement))
                    {
                        // Handle int, decimal, or string tax rate like in GetArticlesAsync
                        if (taxRateElement.ValueKind == JsonValueKind.Number)
                        {
                            if (taxRateElement.TryGetInt32(out var intValue))
                            {
                                taxRatePercentage = intValue;
                            }
                            else if (taxRateElement.TryGetDecimal(out var decimalValue))
                            {
                                taxRatePercentage = (int)decimalValue;
                            }
                        }
                        else if (taxRateElement.ValueKind == JsonValueKind.String &&
                                 int.TryParse(taxRateElement.GetString(), out var parsedValue))
                        {
                            taxRatePercentage = parsedValue;
                        }
                    }
                }

                lineItems.Add(new QuoteLineItemInfo(articleId, name, description, quantity, unitName, unitPrice, taxRatePercentage));
            }
        }

        return new QuoteInfo(quoteId, quoteNumber, link, createdAt, validUntil, status, voucherStatus, lineItems);
    }

    public async Task<string?> GetQuoteLinkAsync(string quoteId, CancellationToken cancellationToken = default)
    {
        var quoteInfo = await GetQuoteAsync(quoteId, cancellationToken);
        return quoteInfo?.Link;
    }

    public async Task<IEnumerable<QuoteInfo>> GetQuotesByContactIdAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Cannot retrieve quotes.");
            return Enumerable.Empty<QuoteInfo>();
        }

        try
        {
            // Lexoffice API endpoint to get all quotations
            // We'll need to filter by contactId client-side since the API doesn't support filtering directly
            var response = await _httpClient.GetAsync("/quotations", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get Lexoffice quotes. Status: {Status}, Error: {Error}", 
                    response.StatusCode, errorContent);
                return Enumerable.Empty<QuoteInfo>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var quotes = new List<QuoteInfo>();

            // The API returns a content array with quotation objects
            if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var quoteElement in content.EnumerateArray())
                {
                    // Check if this quote belongs to the specified contact
                    if (quoteElement.TryGetProperty("address", out var address) &&
                        address.TryGetProperty("contactId", out var contactIdElement))
                    {
                        var quoteContactId = contactIdElement.GetString();
                        if (quoteContactId == contactId)
                        {
                            var quoteId = quoteElement.TryGetProperty("id", out var id) 
                                ? id.GetString() 
                                : null;

                            if (string.IsNullOrEmpty(quoteId))
                                continue;

                            var link = quoteElement.TryGetProperty("files", out var files) && files.GetArrayLength() > 0
                                ? files[0].TryGetProperty("href", out var href) ? href.GetString() : null
                                : null;

                            var quoteNumber = quoteElement.TryGetProperty("voucherNumber", out var voucherNumber)
                                ? voucherNumber.GetString()
                                : null;

                            var status = quoteElement.TryGetProperty("archived", out var archived) && archived.GetBoolean()
                                ? "archived"
                                : "active";

                            var validUntil = quoteElement.TryGetProperty("shippingConditions", out var shippingConditions)
                                && shippingConditions.TryGetProperty("shippingEndDate", out var endDate)
                                ? DateTime.Parse(endDate.GetString() ?? DateTime.UtcNow.ToString())
                                : (DateTime?)null;

                            var createdAt = quoteElement.TryGetProperty("voucherDate", out var voucherDate)
                                ? DateTime.Parse(voucherDate.GetString() ?? DateTime.UtcNow.ToString())
                                : DateTime.UtcNow;

                            quotes.Add(new QuoteInfo(quoteId, quoteNumber, link, createdAt, validUntil, status));
                        }
                    }
                }
            }

            _logger.LogInformation("Retrieved {Count} quotes from Lexoffice for contact {ContactId}", quotes.Count, contactId);
            return quotes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quotes from Lexoffice for contact {ContactId}", contactId);
            return Enumerable.Empty<QuoteInfo>();
        }
    }

    public async Task<IEnumerable<ArticleInfo>> GetArticlesAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync())
        {
            _logger.LogWarning("Lexoffice is not configured. Cannot retrieve articles.");
            return Enumerable.Empty<ArticleInfo>();
        }

        // Use the official articles endpoint: https://api.lexware.io/v1/articles
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl ?? "https://api.lexware.io/v1";
        baseUrl = baseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/articles";

        _logger.LogInformation("Retrieving Lexoffice articles. URL: {Url}", fullUrl);

        var requestUri = new Uri(fullUrl);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get Lexoffice articles. Status: {Status}, Error: {Error}", 
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to get Lexoffice articles: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

        // Articles endpoint returns a paged result with a 'content' array
        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Lexoffice articles response does not contain a 'content' array. Returning empty list.");
            return Enumerable.Empty<ArticleInfo>();
        }

        var articles = new List<ArticleInfo>();
        foreach (var articleElement in content.EnumerateArray())
        {
            try
            {
                var id = articleElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(id))
                {
                    // Without an id we cannot safely reference the article in quotations
                    continue;
                }

            // Article response uses "title" instead of "name"
            var name = articleElement.TryGetProperty("title", out var titleProp) 
                ? titleProp.GetString() 
                : articleElement.TryGetProperty("name", out var nameProp) 
                    ? nameProp.GetString() 
                    : null;
            
            var number = articleElement.TryGetProperty("articleNumber", out var articleNumberProp) 
                ? articleNumberProp.GetString() 
                : articleElement.TryGetProperty("number", out var numberProp) 
                    ? numberProp.GetString() 
                    : null;
            
            var description = articleElement.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

            // Build a display name even if the name field is empty
            var displayName = !string.IsNullOrWhiteSpace(name)
                ? name
                : !string.IsNullOrWhiteSpace(description)
                    ? description
                    : !string.IsNullOrWhiteSpace(number)
                        ? number
                        : id;
            
            // Article response uses "price.netPrice" instead of "unitPrice.netAmount"
            decimal? unitPrice = null;
            if (articleElement.TryGetProperty("price", out var priceProp))
            {
                if (priceProp.TryGetProperty("netPrice", out var netPriceProp))
                {
                    unitPrice = netPriceProp.GetDecimal();
                }
            }
            // Fallback to old structure if new structure is not available
            if (!unitPrice.HasValue && articleElement.TryGetProperty("unitPrice", out var unitPriceProp) &&
                unitPriceProp.TryGetProperty("netAmount", out var netAmountProp))
            {
                unitPrice = netAmountProp.GetDecimal();
            }

            var unitName = articleElement.TryGetProperty("unitName", out var unitNameProp) ? unitNameProp.GetString() : null;
            
            // Article response uses "price.taxRate" instead of "unitPrice.taxRatePercentage"
            int? taxRatePercentage = null;
            if (articleElement.TryGetProperty("price", out var priceProp2))
            {
                if (priceProp2.TryGetProperty("taxRate", out var taxRateProp))
                {
                    // Handle different types: int, decimal, or string
                    if (taxRateProp.ValueKind == JsonValueKind.Number)
                    {
                        if (taxRateProp.TryGetInt32(out var intValue))
                        {
                            taxRatePercentage = intValue;
                        }
                        else if (taxRateProp.TryGetDecimal(out var decimalValue))
                        {
                            taxRatePercentage = (int)decimalValue;
                        }
                    }
                    else if (taxRateProp.ValueKind == JsonValueKind.String)
                    {
                        if (int.TryParse(taxRateProp.GetString(), out var parsedValue))
                        {
                            taxRatePercentage = parsedValue;
                        }
                    }
                }
            }
            // Fallback to old structure if new structure is not available
            if (!taxRatePercentage.HasValue && articleElement.TryGetProperty("unitPrice", out var unitPriceProp2) &&
                unitPriceProp2.TryGetProperty("taxRatePercentage", out var taxRateProp2))
            {
                // Handle different types: int, decimal, or string
                if (taxRateProp2.ValueKind == JsonValueKind.Number)
                {
                    if (taxRateProp2.TryGetInt32(out var intValue))
                    {
                        taxRatePercentage = intValue;
                    }
                    else if (taxRateProp2.TryGetDecimal(out var decimalValue))
                    {
                        taxRatePercentage = (int)decimalValue;
                    }
                }
                else if (taxRateProp2.ValueKind == JsonValueKind.String)
                {
                    if (int.TryParse(taxRateProp2.GetString(), out var parsedValue))
                    {
                        taxRatePercentage = parsedValue;
                    }
                }
            }

                articles.Add(new ArticleInfo(
                    id,
                    number ?? string.Empty,
                    displayName,
                    description,
                    unitPrice,
                    unitName,
                    taxRatePercentage
                ));
            }
            catch (Exception ex)
            {
                var articleId = articleElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
                _logger.LogWarning(ex, "Failed to parse article {ArticleId}. Skipping.", articleId);
                // Continue with next article instead of failing the entire request
                continue;
            }
        }

        _logger.LogInformation("Retrieved {Count} articles from Lexoffice.", articles.Count);
        return articles;
    }
}

