using System.Security.Claims;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace HeckelCrm.Web.Middleware;

public class EnsurePartnerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnsurePartnerMiddleware> _logger;

    public EnsurePartnerMiddleware(RequestDelegate next, ILogger<EnsurePartnerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiClient apiClient, IAuthorizationService authorizationService)
    {
        // Skip middleware for API calls, static files, and authentication endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/api/") || 
            path.StartsWith("/css/") || 
            path.StartsWith("/js/") || 
            path.StartsWith("/lib/") ||
            path.StartsWith("/signin-") ||
            path.StartsWith("/signout-") ||
            path.StartsWith("/account/") ||
            path == "/partner/setup" ||
            path == "/partner/setup/" ||
            path.StartsWith("/partner/setup"))
        {
            await _next(context);
            return;
        }

        // Only process authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Check if user is Admin - Admins don't need a partner ID
            var isAdminResult = await authorizationService.AuthorizeAsync(context.User, "Admin");
            if (isAdminResult.Succeeded)
            {
                // Admin users don't need partner setup, skip middleware
                await _next(context);
                return;
            }

            var entraIdObjectId = context.User.FindFirstValue("oid") ?? 
                                  context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (!string.IsNullOrEmpty(entraIdObjectId))
            {
                try
                {
                    // Check if partner exists
                    var partner = await apiClient.GetPartnerByEntraIdAsync(entraIdObjectId, context.RequestAborted);
                    
                    if (partner == null)
                    {
                        // Partner doesn't exist, redirect to partner setup page
                        context.Response.Redirect("/Partner/Setup");
                        return;
                    }
                    else
                    {
                        // Store partner ID in context for easy access
                        context.Items["PartnerId"] = partner.PartnerId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking partner existence for EntraId: {EntraId}", entraIdObjectId);
                    // Continue with request even if check fails
                }
            }
        }

        await _next(context);
    }
}

