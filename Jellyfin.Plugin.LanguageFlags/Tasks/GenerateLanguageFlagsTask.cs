using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LanguageFlags.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LanguageFlags.Tasks;

public class GenerateLanguageFlagsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<GenerateLanguageFlagsTask> _logger;
    private readonly ILogger<FlagOverlayRenderer> _rendererLogger;

    public GenerateLanguageFlagsTask(
        ILibraryManager libraryManager,
        ILogger<GenerateLanguageFlagsTask> logger,
        ILoggerFactory loggerFactory)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _rendererLogger = loggerFactory.CreateLogger<FlagOverlayRenderer>();
    }

    public string Name => "Generate Language Flags";

    public string Key => "GenerateLanguageFlags";

    public string Description => "Generates poster overlays with language flags.";

    public string Category => "Library";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting language flags generation task");

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            _logger.LogWarning("Plugin instance is null");
            return Task.CompletedTask;
        }

        var renderer = new FlagOverlayRenderer(_libraryManager, _rendererLogger);
        var count = renderer.ProcessAll(plugin.Configuration);

        _logger.LogInformation("Language flags generation completed. Processed {Count} items", count);
        progress.Report(100);
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }
}