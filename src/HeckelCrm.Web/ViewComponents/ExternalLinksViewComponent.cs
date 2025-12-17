using HeckelCrm.Web.Options;
using HeckelCrm.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HeckelCrm.Web.ViewComponents;

public class ExternalLinksViewComponent : ViewComponent
{
    private readonly ExternalLinksService _externalLinksService;
    private readonly IOptions<ExternalLinksOptions> _configOptions;
    private readonly ILogger<ExternalLinksViewComponent> _logger;

    public ExternalLinksViewComponent(
        ExternalLinksService externalLinksService,
        IOptions<ExternalLinksOptions> configOptions,
        ILogger<ExternalLinksViewComponent> logger)
    {
        _externalLinksService = externalLinksService;
        _configOptions = configOptions;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var links = await _externalLinksService.GetExternalLinksAsync(HttpContext.RequestAborted);
            return View(links);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load external links from database, using configuration fallback");
            return View(_configOptions.Value);
        }
    }
}

