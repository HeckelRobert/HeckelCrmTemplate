using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace HeckelCrm.Web.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    [Route("Account/Login")]
    public IActionResult Login(string? returnUrl = null, string? error = null, string? expired = null)
    {
        // Show error message if authentication failed
        if (!string.IsNullOrEmpty(error))
        {
            ViewBag.ErrorMessage = error switch
            {
                "auth_failed" => "Die Anmeldung ist fehlgeschlagen. Bitte versuchen Sie es erneut.",
                "remote_failed" => "Die Anmeldung wurde abgebrochen oder ist fehlgeschlagen.",
                _ => "Ein Fehler ist aufgetreten. Bitte versuchen Sie es erneut."
            };
        }
        
        // Show expired message if token expired
        if (!string.IsNullOrEmpty(expired) && expired == "true")
        {
            ViewBag.InfoMessage = "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.";
        }
        
        // If user is already authenticated, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        
        // Force account selection by adding prompt=select_account
        // Redirect to Home/Index after successful login, not Welcome page
        var redirectUri = returnUrl ?? "/Home/Index";
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = redirectUri,
            Items = { { "prompt", "select_account" } }
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [Authorize]
    [Route("Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        return await LogoutInternal();
    }

    [HttpGet]
    [Authorize]
    [Route("Account/Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        return await LogoutInternal();
    }

    private async Task<IActionResult> LogoutInternal()
    {
        // Clear all cookies first
        foreach (var cookie in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookie);
        }
        
        // Sign out from the application
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        
        // Redirect to Entra ID logout endpoint to clear the session
        var callbackUrl = Url.Action("SignedOut", "Account", null, Request.Scheme) ?? "/";
        
        // Build the Entra ID logout URL
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var tenantId = configuration["AzureAd:TenantId"];
        var postLogoutRedirectUri = callbackUrl;
        
        // Add logout_hint to ensure complete logout
        var logoutUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/logout?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
        
        return Redirect(logoutUrl);
    }

    [HttpGet]
    [Route("Account/SignedOut")]
    public IActionResult SignedOut()
    {
        // Return a page that closes the window
        return View();
    }
}

