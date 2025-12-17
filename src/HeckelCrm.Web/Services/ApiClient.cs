using HeckelCrm.Core.DTOs;
using HeckelCrm.Core.Interfaces;
using static HeckelCrm.Core.Interfaces.ILexofficeService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using System.Text;
using System.Text.Json;

namespace HeckelCrm.Web.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration, 
        ILogger<ApiClient> logger, 
        IHttpContextAccessor httpContextAccessor,
        ITokenAcquisition tokenAcquisition)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _tokenAcquisition = tokenAcquisition;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, cannot retrieve access token");
                return null;
            }

            // Check if user is authenticated
            if (httpContext.User?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("User is not authenticated, cannot retrieve access token");
                return null;
            }

            // Use ITokenAcquisition to get access token for the API
            // This requires the API scope to be configured
            var apiAudience = _configuration["Api:Audience"];
            var apiScope = _configuration["Api:Scope"];
            
            if (string.IsNullOrEmpty(apiAudience) || string.IsNullOrEmpty(apiScope))
            {
                _logger.LogWarning("Api:Audience or Api:Scope is not configured");
                return null;
            }

            // Build the full scope: api://{client-id}/{scope-name}
            var fullScope = $"{apiAudience}/{apiScope}";
            
            try
            {
                // Get access token using ITokenAcquisition
                // This will automatically refresh the token if needed
                var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                    new[] { fullScope },
                    user: httpContext.User);
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogDebug("Successfully retrieved access token using ITokenAcquisition (length: {Length})", accessToken.Length);
                    return accessToken;
                }
            }
            catch (MsalUiRequiredException ex)
            {
                // Token refresh required - user needs to re-authenticate
                _logger.LogWarning(ex, "Token refresh required. Redirecting to login. Error: {Error}", ex.Message);
                // Trigger re-authentication by challenging the user
                await httpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                return null;
            }
            catch (MicrosoftIdentityWebChallengeUserException ex)
            {
                // Challenge user exception - user needs to re-authenticate
                _logger.LogWarning(ex, "User challenge required. Redirecting to login. Error: {Error}", ex.Message);
                await httpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get access token using ITokenAcquisition: {Error}", ex.Message);
                // Don't fallback to stored tokens - they might be expired
                // Instead, try to challenge the user
                if (ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase) || 
                    ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Token-related error detected. Redirecting to login.");
                    await httpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    return null;
                }
            }

            _logger.LogError("Failed to retrieve access token. User authenticated: {IsAuthenticated}", 
                httpContext.User?.Identity?.IsAuthenticated);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token: {Error}", ex.Message);
            return null;
        }
    }

    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Added Authorization header to request for endpoint: {Endpoint}", endpoint);
            }
            else
            {
                _logger.LogWarning("No token available for endpoint: {Endpoint}. Request will be sent without Authorization header.", endpoint);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expired or invalid - trigger re-authentication
                _logger.LogWarning("API endpoint {Endpoint} returned 401 Unauthorized. Token may be expired.", endpoint);
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    // Clear the authentication and redirect to login
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
                return default;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint {Endpoint} returned status {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
                // Throw exception for non-success status codes to allow proper error handling
                throw new HttpRequestException($"API endpoint {endpoint} returned status {response.StatusCode}: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API endpoint: {Endpoint}", endpoint);
            throw;
        }
    }

    private async Task<T?> PostAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : string.Empty;
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("API endpoint {Endpoint} returned empty response", endpoint);
                    return default;
                }
                return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expired or invalid - trigger re-authentication
                _logger.LogWarning("API endpoint {Endpoint} returned 401 Unauthorized. Token may be expired.", endpoint);
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    // Clear the authentication and redirect to login
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
                return default;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint {Endpoint} returned status {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
                // Throw exception for non-success status codes to allow proper error handling
                throw new HttpRequestException($"API endpoint {endpoint} returned status {response.StatusCode}: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API endpoint: {Endpoint}", endpoint);
            throw;
        }
    }

    private async Task<T?> PutAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : string.Empty;
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expired or invalid - trigger re-authentication
                _logger.LogWarning("API endpoint {Endpoint} returned 401 Unauthorized. Token may be expired.", endpoint);
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    // Clear the authentication and redirect to login
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint {Endpoint} returned status {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
            }
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API endpoint: {Endpoint}", endpoint);
            throw;
        }
    }

    // Offers
    public async Task<IEnumerable<OfferDto>?> GetOffersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all offers (Admin endpoint)");
        var result = await GetAsync<IEnumerable<OfferDto>>("/api/offers", cancellationToken);
        _logger.LogInformation("Retrieved {Count} offers", result?.Count() ?? 0);
        return result;
    }

    public async Task<OfferDto?> GetOfferByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetAsync<OfferDto>($"/api/offers/{id}", cancellationToken);
    }

    public async Task<IEnumerable<OfferDto>?> GetOffersByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<OfferDto>>($"/api/offers/partner/{partnerId}", cancellationToken);
    }

    public async Task<bool> SyncOfferWithLexofficeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/offers/{id}/sync-lexoffice");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing offer {OfferId} with Lexoffice", id);
            return false;
        }
    }

    // Partners
    public async Task<PartnerDto?> GetPartnerByEntraIdAsync(string entraIdObjectId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<PartnerDto>($"/api/partners/by-entra-id/{entraIdObjectId}", cancellationToken);
    }

    public async Task<PartnerDto?> GetPartnerByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<PartnerDto>($"/api/partners/by-partner-id/{partnerId}", cancellationToken);
    }

    public async Task<PartnerDto?> CreateOrGetPartnerAsync(CreatePartnerDto dto, CancellationToken cancellationToken = default)
    {
        return await PostAsync<PartnerDto>("/api/partners/create-or-get", dto, cancellationToken);
    }

    public async Task<IEnumerable<PartnerDto>?> GetAllPartnersAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<PartnerDto>>("/api/partners", cancellationToken);
    }

    public async Task<PartnerDto?> UpdatePartnerAsync(string partnerId, UpdatePartnerDto dto, CancellationToken cancellationToken = default)
    {
        return await PutAsync<PartnerDto>($"/api/partners/{partnerId}", dto, cancellationToken);
    }

    // Contacts
    public async Task<IEnumerable<ContactDto>?> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all contacts (Admin endpoint)");
        var result = await GetAsync<IEnumerable<ContactDto>>("/api/contacts", cancellationToken);
        _logger.LogInformation("Retrieved {Count} contacts", result?.Count() ?? 0);
        return result;
    }

    public async Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetAsync<ContactDto>($"/api/contacts/{id}", cancellationToken);
    }

    public async Task<ContactDto?> GetContactForConfirmationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetAsync<ContactDto>($"/api/contacts/{id}/confirmation", cancellationToken);
    }

    public async Task<IEnumerable<ContactDto>?> GetContactsByPartnerIdAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<ContactDto>>($"/api/contacts/partner/{partnerId}", cancellationToken);
    }

    public async Task<(ContactDto? Contact, string? ErrorMessage)> CreateContactWithErrorAsync(CreateContactDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var json = JsonSerializer.Serialize(dto, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/contacts")
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("API endpoint /api/contacts returned empty response");
                    return (null, "Die Anfrage wurde nicht erfolgreich verarbeitet.");
                }
                var contact = JsonSerializer.Deserialize<ContactDto>(responseContent, _jsonOptions);
                return (contact, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint /api/contacts returned status {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                // Try to extract error message from JSON response
                string? errorMessage = null;
                try
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent, _jsonOptions);
                    if (errorJson.TryGetProperty("error", out var errorProp))
                    {
                        errorMessage = errorProp.GetString();
                    }
                }
                catch
                {
                    // If parsing fails, use the raw error content or a default message
                    errorMessage = !string.IsNullOrWhiteSpace(errorContent) ? errorContent : "Ein Fehler ist bei der Anfrage aufgetreten.";
                }
                
                return (null, errorMessage ?? "Ein Fehler ist bei der Anfrage aufgetreten.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API endpoint: /api/contacts");
            return (null, "Ein Fehler ist bei der Anfrage aufgetreten. Bitte versuchen Sie es erneut.");
        }
    }

    public async Task<ContactDto?> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default)
    {
        var (contact, _) = await CreateContactWithErrorAsync(dto, cancellationToken);
        return contact;
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, CreateContactDto dto, CancellationToken cancellationToken = default)
    {
        return await PutAsync<ContactDto>($"/api/contacts/{id}", dto, cancellationToken);
    }

    public async Task<bool> UpdateBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/contacts/{id}/billing-status")
            {
                Content = content
            };
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating billing status for contact {ContactId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/contacts/{id}");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact {ContactId}", id);
            return false;
        }
    }

    public async Task<ContactDto?> CreateLexofficeContactForContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contacts/{contactId}/lexoffice-contact");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint /api/contacts/{ContactId}/lexoffice-contact returned status {Status}: {Error}", 
                    contactId, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ContactDto>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Lexoffice contact for contact {ContactId}", contactId);
            return null;
        }
    }

    // Quote Requests
    public async Task<IEnumerable<QuoteRequestDto>?> GetQuoteRequestsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<QuoteRequestDto>>("/api/quoterequests", cancellationToken);
    }

    public async Task<QuoteRequestDto?> GetQuoteRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetAsync<QuoteRequestDto>($"/api/quoterequests/{id}", cancellationToken);
    }

    public async Task<IEnumerable<QuoteRequestDto>?> GetQuoteRequestsByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<QuoteRequestDto>>($"/api/quoterequests/contact/{contactId}", cancellationToken);
    }

    public async Task<QuoteRequestDto?> CreateQuoteRequestAsync(CreateQuoteRequestDto dto, CancellationToken cancellationToken = default)
    {
        return await PostAsync<QuoteRequestDto>("/api/quoterequests", dto, cancellationToken);
    }

    public async Task<bool> UpdateRequestStatusAsync(Guid id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/quoterequests/{id}/status")
            {
                Content = content
            };
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating request status for quote request {QuoteRequestId}", id);
            return false;
        }
    }

    // Offers (updated)
    public async Task<IEnumerable<OfferDto>?> GetOffersByQuoteRequestIdAsync(Guid quoteRequestId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<OfferDto>>($"/api/offers/quote-request/{quoteRequestId}", cancellationToken);
    }

    public async Task<IEnumerable<OfferDto>?> GetOffersByContactIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<OfferDto>>($"/api/offers/contact/{contactId}", cancellationToken);
    }

    public async Task<IEnumerable<OfferDto>?> LoadOffersFromLexofficeAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/offers/contact/{contactId}/load-from-lexoffice");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("API endpoint /api/offers/contact/{ContactId}/load-from-lexoffice returned status {Status}: {Error}", 
                    contactId, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<IEnumerable<OfferDto>>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading offers from Lexoffice for contact {ContactId}", contactId);
            return null;
        }
    }

    public async Task<IEnumerable<ArticleInfo>?> GetArticlesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<ArticleInfo>>("/api/offers/articles", cancellationToken);
    }

    public async Task<OfferDto?> CreateOfferAsync(CreateOfferDto dto, CancellationToken cancellationToken = default)
    {
        return await PostAsync<OfferDto>("/api/offers", dto, cancellationToken);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateOfferBillingStatusAsync(Guid id, UpdateBillingStatusDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/offers/{id}/billing-status")
            {
                Content = content
            };
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
                return (false, "Nicht autorisiert");
            }
            
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }
            
            // Extract error message from response
            string? errorMessage = null;
            try
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(responseContent))
                {
                    errorMessage = responseContent.Trim('"');
                }
            }
            catch
            {
                // If we can't read the error message, use a default one
            }
            
            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = $"Fehler beim Aktualisieren des Abrechnungsstatus. Status: {response.StatusCode}";
            }
            
            _logger.LogWarning("Failed to update offer billing status: {ErrorMessage}", errorMessage);
            return (false, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating offer billing status");
            return (false, $"Fehler beim Aktualisieren des Abrechnungsstatus: {ex.Message}");
        }
    }

    // ApplicationTypes
    public async Task<IEnumerable<ApplicationTypeDto>?> GetApplicationTypesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<IEnumerable<ApplicationTypeDto>>("/api/applicationtypes", cancellationToken);
    }

    public async Task<ApplicationTypeDto?> GetApplicationTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetAsync<ApplicationTypeDto>($"/api/applicationtypes/{id}", cancellationToken);
    }

    public async Task<ApplicationTypeDto?> CreateApplicationTypeAsync(CreateApplicationTypeDto dto, CancellationToken cancellationToken = default)
    {
        return await PostAsync<ApplicationTypeDto>("/api/applicationtypes", dto, cancellationToken);
    }

    public async Task<ApplicationTypeDto?> UpdateApplicationTypeAsync(Guid id, CreateApplicationTypeDto dto, CancellationToken cancellationToken = default)
    {
        return await PutAsync<ApplicationTypeDto>($"/api/applicationtypes/{id}", dto, cancellationToken);
    }

    public async Task<bool> DeleteApplicationTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/applicationtypes/{id}");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
                return false;
            }
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting application type");
            return false;
        }
    }

    public async Task<bool> BatchSyncOffersWithLexofficeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/offers/batch-sync");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    await httpContext.SignOutAsync("Cookies");
                    httpContext.Response.Redirect("/Account/Login?expired=true");
                }
                return false;
            }
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch syncing offers with Lexoffice");
            return false;
        }
    }

    public async Task<AdminSettingsDto?> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<AdminSettingsDto>("/api/adminsettings", cancellationToken);
    }

    public async Task<AdminSettingsDto?> UpdateAdminSettingsAsync(UpdateAdminSettingsDto dto, CancellationToken cancellationToken = default)
    {
        return await PutAsync<AdminSettingsDto>("/api/adminsettings", dto, cancellationToken);
    }
}

