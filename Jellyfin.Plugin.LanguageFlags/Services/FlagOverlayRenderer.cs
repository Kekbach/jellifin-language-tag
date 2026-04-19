using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.LanguageFlags.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IOPath = System.IO.Path;

namespace Jellyfin.Plugin.LanguageFlags.Services;

public sealed class FlagOverlayRenderer
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FlagOverlayRenderer> _logger;
    private readonly RunStatusStore _runStatusStore = RunStatusStore.Instance;

    public FlagOverlayRenderer(ILibraryManager libraryManager, ILogger<FlagOverlayRenderer> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public int ProcessAll(PluginConfiguration config)
    {
        var items = new List<BaseItem>();

        if (config.ProcessMovies)
        {
            items.AddRange(_libraryManager
                .GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Movie } })
                .OfType<Movie>());
        }

        if (config.ProcessSeries)
        {
            items.AddRange(_libraryManager
                .GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Series } })
                .OfType<Series>());
        }

        var total = items.Count;
        _logger.LogInformation(
            "LanguageFlags ProcessAll started. Movies={Movies}, Series={Series}, TotalItems={Total}",
            config.ProcessMovies,
            config.ProcessSeries,
            total);

        var processed = 0;
        var generated = 0;

        _runStatusStore.Start(total);

        try
        {
            if (total == 0)
            {
                _runStatusStore.Complete("Finished. No matching items found.");
                return 0;
            }

            foreach (var item in items)
            {
                _runStatusStore.UpdateProgress(processed, total, item.Name);
                generated += ProcessItem(item, config);
                processed++;
                _runStatusStore.UpdateProgress(processed, total, item.Name);
            }

            _logger.LogInformation(
                "LanguageFlags ProcessAll finished. Processed={Processed}, Generated={Generated}, Total={Total}",
                processed,
                generated,
                total);

            _runStatusStore.Complete($"Finished. Checked {total} items, generated {generated} images.");
            return generated;
        }
        catch (Exception ex)
        {
            _runStatusStore.Fail($"Failed: {ex.Message}");
            throw;
        }
    }

    private int ProcessItem(BaseItem item, PluginConfiguration config)
    {
        try
        {
            var generated = 0;
            var languages = GetDisplayCountries(item, config).ToList();

            if (config.GeneratePrimary)
            {
                if (languages.Count == 0)
                {
                    _logger.LogInformation("Skipping primary for {Name}: no display languages found.", item.Name);
                }
                else if (TryGeneratePrimaryPoster(item, languages, config))
                {
                    generated++;
                }
            }
            else
            {
                RestoreDefaultPrimary(item, config);
            }

            if (config.GenerateLandscape)
            {
                if (languages.Count == 0)
                {
                    _logger.LogInformation("Skipping landscape for {Name}: no display languages found.", item.Name);
                }
                else if (TryGenerateLandscapeThumb(item, languages, config))
                {
                    generated++;
                }
            }
            else
            {
                RestoreDefaultLandscape(item, config);
            }

            return generated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed generating poster overlay for item {Item}", item.Name);
            return 0;
        }
    }

    private void RestoreDefaultPrimary(BaseItem item, PluginConfiguration config)
    {
        try
        {
            var defaultPoster = FindDefaultPrimaryPosterPath(item, config);
            if (string.IsNullOrWhiteSpace(defaultPoster) || !File.Exists(defaultPoster))
            {
                _logger.LogInformation("No default primary poster found for {Name}.", item.Name);
                return;
            }

            var file = BaseItem.FileSystem.GetFileSystemInfo(defaultPoster);
            item.SetImagePath(ImageType.Primary, 0, file);
            item.DateModified = DateTime.UtcNow;
            item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).GetAwaiter().GetResult();

            _logger.LogInformation("Restored default primary poster for {Name}: {Path}", item.Name, defaultPoster);

            var metadataFolder = GetMetadataFolder(item);
            if (!string.IsNullOrWhiteSpace(metadataFolder))
            {
                var generatedPath = IOPath.Combine(metadataFolder, "poster" + config.MarkerSuffix + ".png");
                if (!string.Equals(defaultPoster, generatedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(generatedPath))
                {
                    File.Delete(generatedPath);
                    _logger.LogInformation("Deleted generated primary poster for {Name}: {Path}", item.Name, generatedPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed restoring default primary poster for item {Item}", item.Name);
        }
    }

    private void RestoreDefaultLandscape(BaseItem item, PluginConfiguration config)
    {
        try
        {
            var metadataFolder = GetMetadataFolder(item);
            if (string.IsNullOrWhiteSpace(metadataFolder))
            {
                _logger.LogInformation("No metadata folder found for restoring landscape of {Name}.", item.Name);
                return;
            }

            var defaultLandscape = FindDefaultLandscapePath(item, config, metadataFolder);
            if (string.IsNullOrWhiteSpace(defaultLandscape) || !File.Exists(defaultLandscape))
            {
                _logger.LogInformation("No default landscape found for {Name}.", item.Name);
                return;
            }

            var file = BaseItem.FileSystem.GetFileSystemInfo(defaultLandscape);
            item.SetImagePath(ImageType.Thumb, 0, file);
            item.SetImagePath(ImageType.Backdrop, 0, file);
            item.DateModified = DateTime.UtcNow;
            item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).GetAwaiter().GetResult();

            _logger.LogInformation("Restored default landscape for {Name}: {Path}", item.Name, defaultLandscape);

            var generatedPath = IOPath.Combine(metadataFolder, "landscape" + config.MarkerSuffix + ".png");
            if (!string.Equals(defaultLandscape, generatedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
                _logger.LogInformation("Deleted generated landscape image for {Name}: {Path}", item.Name, generatedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed restoring default landscape for item {Item}", item.Name);
        }
    }

    private bool TryGeneratePrimaryPoster(BaseItem item, IReadOnlyList<string> languages, PluginConfiguration config)
    {
        var sourceImagePath = ResolveSourcePosterPath(item, config);
        var hasRealPoster = !string.IsNullOrWhiteSpace(sourceImagePath) && File.Exists(sourceImagePath);

        string outputPath;
        Image<Rgba32> poster;

        if (hasRealPoster)
        {
            outputPath = GetGeneratedPrimaryPath(sourceImagePath!, config.MarkerSuffix);
            poster = Image.Load<Rgba32>(sourceImagePath!);
        }
        else
        {
            var metadataFolder = GetMetadataFolder(item);
            if (string.IsNullOrWhiteSpace(metadataFolder))
            {
                _logger.LogInformation("Skipping primary for {Name}: no metadata folder found.", item.Name);
                return false;
            }

            outputPath = IOPath.Combine(metadataFolder, "poster" + config.MarkerSuffix + ".png");
            poster = LoadFallbackPoster();

            _logger.LogInformation("Using fallback poster for {Name}. Output: {Path}", item.Name, outputPath);
        }

        using (poster)
        {
            if (File.Exists(outputPath) && !config.OverwriteExistingGeneratedPrimary)
            {
                _logger.LogInformation(
                    "Skipping primary for {Name}: generated poster already exists at {Path} and overwrite is disabled.",
                    item.Name,
                    outputPath);
                return false;
            }

            ApplyOverlay(
                poster,
                languages,
                config.PrimaryFlagWidthPercent,
                config.PrimaryMarginPercent,
                config.PrimaryBackgroundOpacity,
                config.PrimaryOverlayPosition,
                OverlayImageKind.PrimaryPoster,
                config.PrimaryAnchorX,
                config.PrimaryAnchorY);

            Directory.CreateDirectory(IOPath.GetDirectoryName(outputPath)!);
            poster.Save(outputPath, new PngEncoder());

            var savedFile = BaseItem.FileSystem.GetFileSystemInfo(outputPath);
            item.SetImagePath(ImageType.Primary, 0, savedFile);
            item.DateModified = DateTime.UtcNow;
            item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).GetAwaiter().GetResult();
        }

        _logger.LogInformation("Generated language flags primary poster for {Name}: {Path}", item.Name, outputPath);
        return true;
    }

    private bool TryGenerateLandscapeThumb(BaseItem item, IReadOnlyList<string> languages, PluginConfiguration config)
    {
        var metadataFolder = GetMetadataFolder(item);
        if (string.IsNullOrWhiteSpace(metadataFolder))
        {
            _logger.LogInformation("Skipping landscape for {Name}: no metadata folder found.", item.Name);
            return false;
        }

        var sourceLandscapePath = FindLandscapeSourcePath(item, config, metadataFolder);
        var outputPath = IOPath.Combine(metadataFolder, "landscape" + config.MarkerSuffix + ".png");

        if (File.Exists(outputPath) && !config.OverwriteExistingGeneratedLandscape)
        {
            _logger.LogInformation(
                "Skipping landscape for {Name}: generated image already exists at {Path} and overwrite is disabled.",
                item.Name,
                outputPath);
            return false;
        }

        Image<Rgba32> image;
        var usedFallback = false;

        if (!string.IsNullOrWhiteSpace(sourceLandscapePath) && File.Exists(sourceLandscapePath))
        {
            image = Image.Load<Rgba32>(sourceLandscapePath);
        }
        else
        {
            image = LoadFallbackPoster();
            usedFallback = true;
        }

        using (image)
        {
            ApplyOverlay(
                image,
                languages,
                config.LandscapeFlagWidthPercent,
                config.LandscapeMarginPercent,
                config.LandscapeBackgroundOpacity,
                config.LandscapeOverlayPosition,
                OverlayImageKind.LandscapeThumb,
                config.LandscapeAnchorX,
                config.LandscapeAnchorY);

            Directory.CreateDirectory(IOPath.GetDirectoryName(outputPath)!);
            image.Save(outputPath, new PngEncoder());

            var savedFile = BaseItem.FileSystem.GetFileSystemInfo(outputPath);
            item.SetImagePath(ImageType.Thumb, 0, savedFile);
            item.SetImagePath(ImageType.Backdrop, 0, savedFile);
            item.DateModified = DateTime.UtcNow;
            item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (usedFallback)
        {
            _logger.LogInformation(
                "Generated language flags landscape thumb for {Name} using fallback image: {Path}",
                item.Name,
                outputPath);
        }
        else
        {
            _logger.LogInformation("Generated language flags landscape thumb for {Name}: {Path}", item.Name, outputPath);
        }

        return true;
    }

    private static string? GetMetadataFolder(BaseItem item)
    {
        var primaryInfo = item.GetImageInfo(ImageType.Primary, 0);
        if (primaryInfo is not null && !string.IsNullOrWhiteSpace(primaryInfo.Path))
        {
            return IOPath.GetDirectoryName(primaryInfo.Path);
        }

        var thumbInfo = item.GetImageInfo(ImageType.Thumb, 0);
        if (thumbInfo is not null && !string.IsNullOrWhiteSpace(thumbInfo.Path))
        {
            return IOPath.GetDirectoryName(thumbInfo.Path);
        }

        var itemId = item.Id.ToString("N").ToLowerInvariant();
        if (itemId.Length < 2)
        {
            return null;
        }

        var root = "/config/data/metadata/library";
        var first = itemId.Substring(0, 2);

        return IOPath.Combine(root, first, itemId);
    }

    private static Image<Rgba32> LoadFallbackPoster()
    {
        var assembly = typeof(Plugin).Assembly;
        const string resourceName = "Jellyfin.Plugin.LanguageFlags.Resources.Images.error.png";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new FileNotFoundException($"Embedded fallback poster not found: {resourceName}");
        }

        return Image.Load<Rgba32>(stream);
    }

    private static string? FindDefaultPrimaryPosterPath(BaseItem item, PluginConfiguration config)
    {
        var imageInfo = item.GetImageInfo(ImageType.Primary, 0);
        var primaryPath = imageInfo?.Path;

        if (!string.IsNullOrWhiteSpace(primaryPath) && File.Exists(primaryPath))
        {
            var fileName = IOPath.GetFileName(primaryPath);
            if (!fileName.EndsWith(config.MarkerSuffix + ".png", StringComparison.OrdinalIgnoreCase))
            {
                return primaryPath;
            }

            var dir = IOPath.GetDirectoryName(primaryPath)!;
            var originalPoster = FindOriginalPosterInDirectory(dir, config.MarkerSuffix);
            if (!string.IsNullOrWhiteSpace(originalPoster))
            {
                return originalPoster;
            }
        }

        var metadataFolder = GetMetadataFolder(item);
        if (!string.IsNullOrWhiteSpace(metadataFolder))
        {
            var originalPoster = FindOriginalPosterInDirectory(metadataFolder, config.MarkerSuffix);
            if (!string.IsNullOrWhiteSpace(originalPoster))
            {
                return originalPoster;
            }
        }

        return null;
    }

    private static string? FindDefaultLandscapePath(BaseItem item, PluginConfiguration config, string metadataFolder)
    {
        var thumbInfo = item.GetImageInfo(ImageType.Thumb, 0);
        var thumbPath = thumbInfo?.Path;

        if (!string.IsNullOrWhiteSpace(thumbPath) && File.Exists(thumbPath))
        {
            var fileName = IOPath.GetFileName(thumbPath);
            if (!fileName.EndsWith(config.MarkerSuffix + ".png", StringComparison.OrdinalIgnoreCase))
            {
                return thumbPath;
            }
        }

        var candidates = new[]
        {
            IOPath.Combine(metadataFolder, "landscape.jpg"),
            IOPath.Combine(metadataFolder, "landscape.png"),
            IOPath.Combine(metadataFolder, "landscape.jpeg"),
            IOPath.Combine(metadataFolder, "landscape.webp")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindLandscapeSourcePath(BaseItem item, PluginConfiguration config, string metadataFolder)
    {
        var thumbInfo = item.GetImageInfo(ImageType.Thumb, 0);
        var thumbPath = thumbInfo?.Path;

        if (!string.IsNullOrWhiteSpace(thumbPath) && File.Exists(thumbPath))
        {
            var thumbFileName = IOPath.GetFileName(thumbPath);

            if (!thumbFileName.EndsWith(config.MarkerSuffix + ".png", StringComparison.OrdinalIgnoreCase))
            {
                return thumbPath;
            }
        }

        var candidates = new[]
        {
            IOPath.Combine(metadataFolder, "landscape.jpg"),
            IOPath.Combine(metadataFolder, "landscape.png"),
            IOPath.Combine(metadataFolder, "landscape.jpeg"),
            IOPath.Combine(metadataFolder, "landscape.webp")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindOriginalPosterInDirectory(string directory, string markerSuffix)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = new[]
        {
            "poster.jpg",
            "poster.png",
            "poster.jpeg",
            "poster.webp"
        };

        foreach (var candidate in candidates)
        {
            var path = IOPath.Combine(directory, candidate);
            if (!File.Exists(path))
            {
                continue;
            }

            var fileName = IOPath.GetFileName(path);
            if (fileName.EndsWith(markerSuffix + ".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return path;
        }

        return null;
    }

    private static string? ResolveSourcePosterPath(BaseItem item, PluginConfiguration config)
    {
        var imageInfo = item.GetImageInfo(ImageType.Primary, 0);
        var primaryPath = imageInfo?.Path;

        if (!string.IsNullOrWhiteSpace(primaryPath) && File.Exists(primaryPath))
        {
            var fileName = IOPath.GetFileName(primaryPath);

            if (!fileName.EndsWith(config.MarkerSuffix + ".png", StringComparison.OrdinalIgnoreCase))
            {
                return primaryPath;
            }

            var dir = IOPath.GetDirectoryName(primaryPath)!;
            var originalPoster = FindOriginalPosterInDirectory(dir, config.MarkerSuffix);
            if (!string.IsNullOrWhiteSpace(originalPoster))
            {
                return originalPoster;
            }

            return null;
        }

        var metadataFolder = GetMetadataFolder(item);
        if (!string.IsNullOrWhiteSpace(metadataFolder))
        {
            var originalPoster = FindOriginalPosterInDirectory(metadataFolder, config.MarkerSuffix);
            if (!string.IsNullOrWhiteSpace(originalPoster))
            {
                return originalPoster;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetDisplayCountries(BaseItem item, PluginConfiguration config)
    {
        var all = GetLanguages(item, config).ToList();
        if (all.Count == 0)
        {
            return all;
        }

        var maxFlags = Math.Max(1, config.MaxFlags);
        var prioritized = new List<string>();

        var preferred = (config.PreferredLanguages ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToList();

        void AddIfPresent(string code)
        {
            var existing = all.FirstOrDefault(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(existing) &&
                !prioritized.Any(x => string.Equals(x, existing, StringComparison.OrdinalIgnoreCase)))
            {
                prioritized.Add(existing);
            }
        }

        foreach (var code in preferred)
        {
            AddIfPresent(code);
        }

        foreach (var code in all)
        {
            if (!prioritized.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
            {
                prioritized.Add(code);
            }
        }

        if (prioritized.Count <= maxFlags)
        {
            return prioritized;
        }

        var result = prioritized.Take(maxFlags).ToList();
        result.Add("plus");
        return result;
    }

    private static string GetGeneratedPrimaryPath(string originalPath, string markerSuffix)
    {
        var dir = IOPath.GetDirectoryName(originalPath)!;
        return IOPath.Combine(dir, "poster" + markerSuffix + ".png");
    }

    private static IEnumerable<string> GetLanguages(BaseItem item, PluginConfiguration config)
    {
        static IEnumerable<string> ExtractLanguages(IEnumerable<MediaStream> streams, PluginConfiguration cfg)
        {
            var ordered = new List<string>();

            if (cfg.PreferAudioLanguages)
            {
                ordered.AddRange(streams.Where(s => s.Type == MediaStreamType.Audio).Select(s => s.Language));
                if (cfg.IncludeSubtitlesIfNoAudio && ordered.Count == 0)
                {
                    ordered.AddRange(streams.Where(s => s.Type == MediaStreamType.Subtitle).Select(s => s.Language));
                }
            }
            else
            {
                ordered.AddRange(streams.Select(s => s.Language));
            }

            return ordered
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(LanguageMapping.ToCountryCode)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        var direct = ExtractLanguages(item.GetMediaStreams(), config).ToList();
        if (direct.Count > 0)
        {
            return direct;
        }

        if (item is Series series)
        {
            var episodeLanguages = series.RecursiveChildren
                .OfType<Episode>()
                .Take(25)
                .SelectMany(e => ExtractLanguages(e.GetMediaStreams(), config))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (episodeLanguages.Count > 0)
            {
                return episodeLanguages;
            }
        }

        return Enumerable.Empty<string>();
    }

    private static void ApplyOverlay(
        Image<Rgba32> image,
        IReadOnlyList<string> countries,
        int flagWidthPercent,
        int marginPercent,
        double backgroundOpacity,
        string? overlayPosition,
        OverlayImageKind imageKind,
        float anchorX,
        float anchorY)
    {
        if (countries.Count == 0)
        {
            return;
        }

        var profile = GetOverlayProfile(imageKind);

        float scaleX = image.Width / profile.StandardWidth;

        float virtualFlagWidth = Math.Max(48f, profile.StandardWidth * flagWidthPercent / 100f);
        int flagWidth = Math.Max(48, (int)Math.Round(virtualFlagWidth * scaleX));
        int flagHeight = Math.Max(28, (int)Math.Round(flagWidth * 0.66f));

        float virtualMargin = Math.Max(8f, profile.StandardWidth * marginPercent / 100f);
        int padding = Math.Max(6, (int)Math.Round(virtualMargin * 0.55f * scaleX));
        int gap = Math.Max(6, (int)Math.Round(flagHeight * 0.2f));

        int totalHeight = (flagHeight * countries.Count) + (gap * (countries.Count - 1));
        int panelWidth = flagWidth + (padding * 2);
        int panelHeight = totalHeight + (padding * 2);

        float centerX = image.Width / 2f;
        float centerY = image.Height / 2f;

        var (offsetXFactor, offsetYFactor) = GetOverlayAnchorFactors(
            overlayPosition,
            anchorX,
            anchorY);

        float anchorPointX = centerX + (offsetXFactor * image.Width);
        float anchorPointY = centerY + (offsetYFactor * image.Height);

        int panelX = offsetXFactor < 0
            ? (int)Math.Round(anchorPointX)
            : (int)Math.Round(anchorPointX - panelWidth);

        int panelY = offsetYFactor < 0
            ? (int)Math.Round(anchorPointY)
            : (int)Math.Round(anchorPointY - panelHeight);

        panelX = Math.Max(0, Math.Min(panelX, image.Width - panelWidth));
        panelY = Math.Max(0, Math.Min(panelY, image.Height - panelHeight));

        image.Mutate(ctx =>
        {
            var badge = CreateRoundedRectangle(
                panelX,
                panelY,
                panelWidth,
                panelHeight,
                Math.Max(8, padding));

            ctx.Fill(Color.Black.WithAlpha((float)backgroundOpacity), badge);

            for (var i = 0; i < countries.Count; i++)
            {
                int x = panelX + padding;
                int y = panelY + padding + (i * (flagHeight + gap));

                using var flag = DrawFlag(countries[i], flagWidth, flagHeight);
                ctx.DrawImage(flag, new Point(x, y), 1f);
            }
        });
    }

    private static OverlayProfile GetOverlayProfile(OverlayImageKind imageKind)
    {
        return imageKind switch
        {
            OverlayImageKind.PrimaryPoster => new OverlayProfile(1000f, 1500f),
            OverlayImageKind.LandscapeThumb => new OverlayProfile(1600f, 900f),
            _ => throw new ArgumentOutOfRangeException(nameof(imageKind), imageKind, null)
        };
    }

    private static (float OffsetXFactor, float OffsetYFactor) GetOverlayAnchorFactors(
        string? overlayPosition,
        float anchorX,
        float anchorY)
    {
        var position = (overlayPosition ?? "top-left").Trim().ToLowerInvariant();

        return position switch
        {
            "top-right" => (+anchorX, -anchorY),
            "bottom-right" => (+anchorX, +anchorY),
            "bottom-left" => (-anchorX, +anchorY),
            "top-left" => (-anchorX, -anchorY),
            _ => (-anchorX, -anchorY)
        };
    }

    private readonly record struct OverlayProfile(float StandardWidth, float StandardHeight);

    private static IPath CreateRoundedRectangle(float x, float y, float width, float height, float radius)
    {
        radius = MathF.Max(0, MathF.Min(radius, MathF.Min(width, height) / 2f));

        var builder = new PathBuilder();

        builder.AddLine(new PointF(x + radius, y), new PointF(x + width - radius, y));
        builder.AddArc(new PointF(x + width - radius, y + radius), radius, radius, 270, 90, 0);

        builder.AddLine(new PointF(x + width, y + radius), new PointF(x + width, y + height - radius));
        builder.AddArc(new PointF(x + width - radius, y + height - radius), radius, radius, 0, 90, 0);

        builder.AddLine(new PointF(x + width - radius, y + height), new PointF(x + radius, y + height));
        builder.AddArc(new PointF(x + radius, y + height - radius), radius, radius, 90, 90, 0);

        builder.AddLine(new PointF(x, y + height - radius), new PointF(x, y + radius));
        builder.AddArc(new PointF(x + radius, y + radius), radius, radius, 180, 90, 0);

        builder.CloseFigure();

        return builder.Build();
    }

    private static Image<Rgba32>? TryLoadEmbeddedFlag(string countryCode, int width, int height)
    {
        var code = countryCode.ToLowerInvariant();
        var resourceName = $"Jellyfin.Plugin.LanguageFlags.Resources.Flags.{code}.png";

        var assembly = typeof(Plugin).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var image = Image.Load<Rgba32>(stream);
        image.Mutate(x => x.Resize(width, height));
        return image;
    }

    private static Image<Rgba32> DrawFlag(string countryCode, int width, int height)
    {
        countryCode = countryCode.ToUpperInvariant();

        var embedded = TryLoadEmbeddedFlag(countryCode, width, height);
        if (embedded is not null)
        {
            embedded.Mutate(ctx =>
            {
                ctx.Draw(Color.White, 2, new RectangularPolygon(1, 1, width - 2, height - 2));
            });

            return embedded;
        }

        var img = new Image<Rgba32>(width, height, Color.White);

        img.Mutate(ctx =>
        {
            switch (countryCode)
            {
                case "PLUS":
                    ctx.Fill(Color.DarkSlateGray);
                    var plusThickness = Math.Max(4, height / 10f);
                    var plusLen = Math.Min(width, height) * 0.45f;
                    var cx = width / 2f;
                    var cy = height / 2f;
                    ctx.Fill(Color.White, new RectangularPolygon(cx - plusThickness / 2f, cy - plusLen / 2f, plusThickness, plusLen));
                    ctx.Fill(Color.White, new RectangularPolygon(cx - plusLen / 2f, cy - plusThickness / 2f, plusLen, plusThickness));
                    break;
                case "DE":
                    HStripes(ctx, width, height, Color.Black, Color.Red, Color.Gold); break;
                case "NL":
                    HStripes(ctx, width, height, Color.Red, Color.White, Color.RoyalBlue); break;
                case "FR":
                    VStripes(ctx, width, height, Color.RoyalBlue, Color.White, Color.Red); break;
                case "IT":
                    VStripes(ctx, width, height, Color.ForestGreen, Color.White, Color.Red); break;
                case "ES":
                    HStripes(ctx, width, height, Color.Red, Color.Gold, Color.Red, 0.25f, 0.5f, 0.25f); break;
                case "PL":
                    HStripes(ctx, width, height, Color.White, Color.HotPink); break;
                case "UA":
                    HStripes(ctx, width, height, Color.DeepSkyBlue, Color.Gold); break;
                case "RU":
                    HStripes(ctx, width, height, Color.White, Color.RoyalBlue, Color.Red); break;
                case "JP":
                    ctx.Fill(Color.White);
                    ctx.Fill(Color.Crimson, new EllipsePolygon(width / 2f, height / 2f, Math.Min(width, height) * 0.22f));
                    break;
                case "BE":
                    VStripes(ctx, width, height, Color.Black, Color.Gold, Color.Red); break;
                case "IE":
                    VStripes(ctx, width, height, Color.ForestGreen, Color.White, Color.Orange); break;
                case "AT":
                    HStripes(ctx, width, height, Color.Red, Color.White, Color.Red); break;
                case "EE":
                    HStripes(ctx, width, height, Color.DeepSkyBlue, Color.Black, Color.White); break;
                case "LT":
                    HStripes(ctx, width, height, Color.Gold, Color.ForestGreen, Color.Red); break;
                case "LU":
                    HStripes(ctx, width, height, Color.Red, Color.White, Color.LightSkyBlue); break;
                case "CZ":
                    ctx.Fill(Color.White);
                    ctx.Fill(Color.Red, new RectangularPolygon(0, height / 2f, width, height / 2f));
                    ctx.Fill(Color.RoyalBlue, new Polygon(new LinearLineSegment(new PointF[] { new(0, 0), new(width * 0.42f, height / 2f), new(0, height) })));
                    break;
                case "FI":
                    NordicCross(ctx, width, height, Color.White, Color.RoyalBlue); break;
                case "SE":
                    NordicCross(ctx, width, height, Color.RoyalBlue, Color.Gold); break;
                case "DK":
                    NordicCross(ctx, width, height, Color.Red, Color.White); break;
                case "NO":
                    NordicCrossTriple(ctx, width, height, Color.Red, Color.White, Color.RoyalBlue); break;
                case "TR":
                    ctx.Fill(Color.Red);
                    ctx.Fill(Color.White, new EllipsePolygon(width * 0.38f, height * 0.5f, height * 0.24f));
                    ctx.Fill(Color.Red, new EllipsePolygon(width * 0.42f, height * 0.5f, height * 0.19f));
                    break;
                default:
                    ctx.Fill(Color.DarkSlateGray);
                    var family = SystemFonts.Families.First();
                    var font = family.CreateFont(height * 0.45f, FontStyle.Bold);
                    var options = new RichTextOptions(font)
                    {
                        Origin = new PointF(width * 0.16f, height * 0.18f)
                    };
                    ctx.DrawText(options, countryCode, Color.White);
                    break;
            }

            ctx.Draw(Color.White, 2, new RectangularPolygon(1, 1, width - 2, height - 2));
        });

        return img;
    }

    private static void HStripes(IImageProcessingContext ctx, int width, int height, params Color[] colors)
        => HStripes(
            ctx,
            width,
            height,
            colors[0],
            colors.Length > 1 ? colors[1] : colors[0],
            colors.Length > 2 ? colors[2] : colors[0],
            1f / colors.Length,
            1f / colors.Length,
            1f / colors.Length);

    private static void HStripes(IImageProcessingContext ctx, int width, int height, Color c1, Color c2)
    {
        ctx.Fill(c1, new RectangularPolygon(0, 0, width, height / 2f));
        ctx.Fill(c2, new RectangularPolygon(0, height / 2f, width, height / 2f));
    }

    private static void HStripes(IImageProcessingContext ctx, int width, int height, Color c1, Color c2, Color c3)
    {
        ctx.Fill(c1, new RectangularPolygon(0, 0, width, height / 3f));
        ctx.Fill(c2, new RectangularPolygon(0, height / 3f, width, height / 3f));
        ctx.Fill(c3, new RectangularPolygon(0, 2 * height / 3f, width, height / 3f));
    }

    private static void HStripes(IImageProcessingContext ctx, int width, int height, Color c1, Color c2, Color c3, float h1, float h2, float h3)
    {
        var y1 = 0f;
        var y2 = height * h1;
        var y3 = height * (h1 + h2);

        ctx.Fill(c1, new RectangularPolygon(0, y1, width, height * h1));
        ctx.Fill(c2, new RectangularPolygon(0, y2, width, height * h2));
        ctx.Fill(c3, new RectangularPolygon(0, y3, width, height * h3));
    }

    private static void VStripes(IImageProcessingContext ctx, int width, int height, Color c1, Color c2, Color c3)
    {
        ctx.Fill(c1, new RectangularPolygon(0, 0, width / 3f, height));
        ctx.Fill(c2, new RectangularPolygon(width / 3f, 0, width / 3f, height));
        ctx.Fill(c3, new RectangularPolygon(2 * width / 3f, 0, width / 3f, height));
    }

    private static void NordicCross(IImageProcessingContext ctx, int width, int height, Color baseColor, Color crossColor)
    {
        ctx.Fill(baseColor);
        ctx.Fill(crossColor, new RectangularPolygon(width * 0.28f, 0, width * 0.14f, height));
        ctx.Fill(crossColor, new RectangularPolygon(0, height * 0.43f, width, height * 0.14f));
    }

    private static void NordicCrossTriple(IImageProcessingContext ctx, int width, int height, Color baseColor, Color outerCross, Color innerCross)
    {
        NordicCross(ctx, width, height, baseColor, outerCross);
        ctx.Fill(innerCross, new RectangularPolygon(width * 0.31f, 0, width * 0.08f, height));
        ctx.Fill(innerCross, new RectangularPolygon(0, height * 0.46f, width, height * 0.08f));
    }
}