using HeckelCrm.Web.Middleware;
using HeckelCrm.Web.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Forwarded Headers for reverse proxy (Nginx)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// Configure API client
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001";
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register HttpContextAccessor for ApiClient
builder.Services.AddHttpContextAccessor();

// Register API client service
builder.Services.AddScoped<HeckelCrm.Web.Services.ApiClient>();

// Register External Links Service
builder.Services.AddScoped<HeckelCrm.Web.Services.ExternalLinksService>();

// Configure external links options (fallback)
builder.Services.Configure<ExternalLinksOptions>(
    builder.Configuration.GetSection(ExternalLinksOptions.SectionName));

// Authentication with Entra ID
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
        // Save tokens to enable API calls
        options.SaveTokens = true;
        
        // Request API scope to get access token for the API
        var apiAudience = builder.Configuration["Api:Audience"];
        var apiScope = builder.Configuration["Api:Scope"];
        
        // Validate that audience is configured and not a placeholder
        // Skip API scope if audience contains placeholder values
        if (!string.IsNullOrEmpty(apiAudience) && 
            !apiAudience.Contains("your-") && 
            !apiAudience.Contains("example") &&
            !string.IsNullOrEmpty(apiScope))
        {
            // Add the API scope to the scopes list
            // This will request an access token for the API during login
            // Format: api://{client-id}/{scope-name}
            options.Scope.Add($"{apiAudience}/{apiScope}");
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        }
        else if (!string.IsNullOrEmpty(apiAudience) && 
                 !apiAudience.Contains("your-") && 
                 !apiAudience.Contains("example"))
        {
            // Fallback: use default scope if not configured
            options.Scope.Add($"{apiAudience}/.default");
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        }
        else
        {
            // Only add basic scopes if API is not configured
            // This allows login to work even if API configuration is missing
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        }
        
        // Always prompt for account selection when redirecting to identity provider
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Redirecting to identity provider. RedirectUri: {RedirectUri}", context.Properties.RedirectUri);
            
            // Always add prompt=select_account to force account selection
            // This prevents automatic login with cached credentials
            // Only override if not explicitly set via properties
            if (!context.Properties.Items.ContainsKey("prompt"))
            {
                context.ProtocolMessage.SetParameter("prompt", "select_account");
            }
            else
            {
                // If we're explicitly logging in, use the prompt from properties
                context.ProtocolMessage.SetParameter("prompt", context.Properties.Items["prompt"]);
            }
            
            return Task.CompletedTask;
        };
        
        // Handle successful authentication callback
        options.Events.OnAuthorizationCodeReceived = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Authorization code received. RedirectUri: {RedirectUri}", context.Properties?.RedirectUri ?? "null");
            
            // Log requested scopes
            var requestedScopes = context.ProtocolMessage?.Scope?.Split(' ') ?? Array.Empty<string>();
            logger.LogInformation("Requested scopes: {Scopes}", string.Join(", ", requestedScopes));
            
            // Log the authorization code (first 20 chars for debugging)
            var authCode = context.ProtocolMessage?.Code;
            if (!string.IsNullOrEmpty(authCode))
            {
                logger.LogDebug("Authorization code received (length: {Length})", authCode.Length);
            }
            
            // The access token should be available after token exchange
            // Microsoft.Identity.Web handles this automatically when SaveTokens = true
            // But we need to ensure the scope is correctly requested
            await Task.CompletedTask;
        };
        
        // Log when tokens are received
        options.Events.OnTokenResponseReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token response received");
            
            // Log available tokens from the response
            if (context.TokenEndpointResponse != null)
            {
                var accessToken = context.TokenEndpointResponse.GetParameter("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    logger.LogInformation("Access token received in token response (length: {Length})", accessToken.Length);
                }
                else
                {
                    logger.LogWarning("No access_token in token endpoint response. Available parameters: {Params}", 
                        string.Join(", ", context.TokenEndpointResponse.Parameters.Select(p => p.Key)));
                }
            }
            
            return Task.CompletedTask;
        };
        
        // Transform claims after token validation to ensure groups are available
        options.Events.OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            
            // Log all claims for debugging
            logger.LogInformation("User authenticated. Claims:");
            foreach (var claim in context.Principal?.Claims ?? Enumerable.Empty<System.Security.Claims.Claim>())
            {
                logger.LogInformation("  Claim Type: {Type}, Value: {Value}", claim.Type, claim.Value);
            }
            
            // Log available tokens from authentication properties
            var tokens = context.Properties?.GetTokens();
            if (tokens != null && tokens.Any())
            {
                logger.LogInformation("Available tokens after validation: {TokenCount}", tokens.Count());
                foreach (var token in tokens)
                {
                    var preview = token.Value?.Length > 50 
                        ? token.Value.Substring(0, 50) + "..." 
                        : token.Value ?? "null";
                    logger.LogInformation("  Token: {Name} = {Preview}", token.Name, preview);
                }
            }
            else
            {
                logger.LogWarning("No tokens found in authentication properties after validation");
            }
            
            // Check for groups in different claim types
            var adminGroupId = configuration["AzureAd:AdminGroupId"];
            var partnerGroupId = configuration["AzureAd:PartnerGroupId"];
            
            if (!string.IsNullOrEmpty(adminGroupId))
            {
                // Check if user is in admin group (check multiple claim types)
                var isInAdminGroup = context.Principal?.HasClaim("groups", adminGroupId) == true ||
                                     context.Principal?.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", adminGroupId) == true ||
                                     context.Principal?.HasClaim("group", adminGroupId) == true;
                
                if (isInAdminGroup)
                {
                    logger.LogInformation("User is in Admin group: {GroupId}", adminGroupId);
                    // Add role claim for easier checking
                    var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                    if (identity != null && !identity.HasClaim(System.Security.Claims.ClaimTypes.Role, "Admin"))
                    {
                        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"));
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(partnerGroupId))
            {
                // Check if user is in partner group (check multiple claim types)
                var isInPartnerGroup = context.Principal?.HasClaim("groups", partnerGroupId) == true ||
                                       context.Principal?.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", partnerGroupId) == true ||
                                       context.Principal?.HasClaim("group", partnerGroupId) == true;
                
                if (isInPartnerGroup)
                {
                    logger.LogInformation("User is in Partner group: {GroupId}", partnerGroupId);
                    // Add role claim for easier checking
                    var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                    if (identity != null && !identity.HasClaim(System.Security.Claims.ClaimTypes.Role, "Partner"))
                    {
                        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Partner"));
                    }
                }
            }
            
            return Task.CompletedTask;
        };
        
        // Handle authentication failures
        options.Events.OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "Authentication failed");
            context.Response.Redirect("/Account/Login?error=auth_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        
        // Handle remote failure (e.g., user cancels login)
        options.Events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var errorMessage = context.Failure?.Message ?? "Unknown error";
            logger.LogError("Remote authentication failed: {Error}", errorMessage);
            context.Response.Redirect("/Account/Login?error=remote_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        
        // Clear cookies on sign out
        options.Events.OnSignedOutCallbackRedirect = context =>
        {
            context.Response.Cookies.Delete(".AspNetCore.Cookies");
            context.Response.Cookies.Delete(".AspNetCore.OpenIdConnect");
            return Task.CompletedTask;
        };
    })
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// Configure cookie authentication to prevent automatic login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    // Required for external OIDC behind reverse proxy (Entra ID + HTTPS)
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Authorization with role-based policies
builder.Services.AddAuthorization(options =>
{
    // Partner Policy: Requires Partner role or Admin role
    options.AddPolicy("Partner", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var partnerGroupId = builder.Configuration["AzureAd:PartnerGroupId"] ?? "";
            var adminGroupId = builder.Configuration["AzureAd:AdminGroupId"] ?? "";
            
            // Check multiple claim types for groups
            var hasPartnerGroup = !string.IsNullOrEmpty(partnerGroupId) && (
                context.User.HasClaim("groups", partnerGroupId) ||
                context.User.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", partnerGroupId) ||
                context.User.HasClaim("group", partnerGroupId));
            
            var hasAdminGroup = !string.IsNullOrEmpty(adminGroupId) && (
                context.User.HasClaim("groups", adminGroupId) ||
                context.User.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", adminGroupId) ||
                context.User.HasClaim("group", adminGroupId));
            
            // Check if user has Partner or Admin role (from claims transformation)
            return hasPartnerGroup || hasAdminGroup ||
                   context.User.IsInRole("Partner") ||
                   context.User.IsInRole("Admin");
        });
    });

    // Admin Policy: Requires Admin role
    options.AddPolicy("Admin", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var adminGroupId = builder.Configuration["AzureAd:AdminGroupId"] ?? "";
            
            // Check multiple claim types for groups
            var hasAdminGroup = !string.IsNullOrEmpty(adminGroupId) && (
                context.User.HasClaim("groups", adminGroupId) ||
                context.User.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups", adminGroupId) ||
                context.User.HasClaim("group", adminGroupId));
            
            // Check if user has Admin role (from claims transformation)
            return hasAdminGroup || context.User.IsInRole("Admin");
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// IMPORTANT: must be before UseHttpsRedirection to honor X-Forwarded-Proto
app.UseForwardedHeaders();

// Configure HTTPS redirection for reverse proxy (Nginx handles HTTPS, so we only redirect in non-production)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// In production, Nginx handles HTTPS, so we don't need HTTPS redirection
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<EnsurePartnerMiddleware>();

// Map MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");

app.Run();

