using HeckelCrm.Api.Options;
using HeckelCrm.Core.Services;
using HeckelCrm.Core.Interfaces;
using HeckelCrm.Infrastructure.Data;
using HeckelCrm.Infrastructure.Options;
using HeckelCrm.Infrastructure.Repositories;
using HeckelCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json if it exists (for local development)
var localConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.Local.json");
if (File.Exists(localConfigPath))
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Authentication with Entra ID
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Configure JWT Bearer options to accept multiple audiences
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Accept both ClientId and api://ClientId as valid audiences
    // This allows tokens from the web app (which may use ClientId) to be accepted
    var clientId = builder.Configuration["AzureAd:ClientId"];
    var configuredAudience = builder.Configuration["AzureAd:Audience"];
    
    if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(configuredAudience))
    {
        options.TokenValidationParameters.ValidAudiences = new[]
        {
            configuredAudience, // api://{client-id}
            clientId // {client-id} (fallback for tokens without api:// prefix)
        };
    }
});

// Authorization with role-based policies
builder.Services.AddAuthorization(options =>
{
    // Partner Policy: Requires Partner role or Admin role
    options.AddPolicy("Partner", policy =>
    {
        policy.RequireAssertion(context =>
        {
            // Check if user has Partner or Admin role
            return context.User.HasClaim("groups", builder.Configuration["AzureAd:PartnerGroupId"] ?? "") ||
                   context.User.HasClaim("groups", builder.Configuration["AzureAd:AdminGroupId"] ?? "") ||
                   context.User.IsInRole("Partner") ||
                   context.User.IsInRole("Admin");
        });
    });

    // Admin Policy: Requires Admin role
    options.AddPolicy("Admin", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var adminGroupId = builder.Configuration["AzureAd:AdminGroupId"];
            // Check if user has Admin role
            return context.User.HasClaim("groups", adminGroupId ?? "") ||
                   context.User.IsInRole("Admin");
        });
    });

    // Default: Require authentication
    options.FallbackPolicy = options.DefaultPolicy;
});

// Register repositories
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IQuoteRequestRepository, QuoteRequestRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IApplicationTypeRepository, ApplicationTypeRepository>();
builder.Services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();

// Register services
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IQuoteRequestService, QuoteRequestService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IApplicationTypeService, ApplicationTypeService>();
builder.Services.AddScoped<IAdminSettingsService, AdminSettingsService>();

// Configure Lexoffice options
builder.Services.Configure<LexofficeOptions>(
    builder.Configuration.GetSection(LexofficeOptions.SectionName));

// Configure external links options
builder.Services.Configure<ExternalLinksOptions>(
    builder.Configuration.GetSection(ExternalLinksOptions.SectionName));

// Register Lexoffice service
var lexofficeBaseUrl = builder.Configuration["Lexoffice:BaseUrl"] ?? "https://api.lexware.io/v1";
builder.Services.AddHttpClient<ILexofficeService, LexofficeService>(client =>
{
    client.BaseAddress = new Uri(lexofficeBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// CORS configuration (optional, for external website integrations)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowExternal", policy =>
    {
        var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? Array.Empty<string>();
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Map OpenAPI endpoint (replaces Swashbuckle)
    app.MapOpenApi().WithName("OpenAPI");
}

app.UseHttpsRedirection();
app.UseCors("AllowExternal");
app.UseAuthentication();
app.UseAuthorization();

// Map API controllers
app.MapControllers();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

app.Run();

