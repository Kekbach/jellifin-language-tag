using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.LanguageFlags.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LanguageFlags.Api;

[ApiController]
[Authorize]
[Route("LanguageFlagsOverlay")]
public class LanguageFlagsController : ControllerBase
{
    private readonly ILogger<LanguageFlagsController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILibraryManager _libraryManager;

    public LanguageFlagsController(
        ILogger<LanguageFlagsController> logger,
        ILoggerFactory loggerFactory,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _libraryManager = libraryManager;
    }

    [HttpPost("Run")]
    public ActionResult Run()
    {
        if (RunStatusStore.Instance.IsRunning)
        {
            return Ok(new { started = false, message = "Already running." });
        }

        var config = Plugin.Instance!.Configuration;

        // Sofort als laufend markieren, damit das erste Status-Polling nicht direkt "finished" sieht
        RunStatusStore.Instance.Start(0);

        _ = Task.Run(() =>
        {
            try
            {
                var rendererLogger = _loggerFactory.CreateLogger<FlagOverlayRenderer>();
                var renderer = new FlagOverlayRenderer(_libraryManager, rendererLogger);
                renderer.ProcessAll(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual run failed.");
                RunStatusStore.Instance.Fail($"Failed: {ex.Message}");
            }
        });

        return Ok(new { started = true });
    }

    [HttpGet("Status")]
    public ActionResult GetStatus()
    {
        return Ok(RunStatusStore.Instance.Snapshot());
    }
}