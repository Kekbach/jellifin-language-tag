using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LanguageFlags.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool ProcessMovies { get; set; } = true;
    public bool ProcessSeries { get; set; } = true;
    public bool PreferAudioLanguages { get; set; } = true;
    public bool IncludeSubtitlesIfNoAudio { get; set; } = true;
    public string PreferredLanguages { get; set; } = "de,gb";
    public int MaxFlags { get; set; } = 3;
    public string MarkerSuffix { get; set; } = ".langflags";

    public bool GeneratePrimary { get; set; } = true;
    public bool OverwriteExistingGeneratedPrimary { get; set; } = true;
    public string PrimaryOverlayPosition { get; set; } = "top-left";
    public int PrimaryFlagWidthPercent { get; set; } = 12;
    public int PrimaryMarginPercent { get; set; } = 2;
    public double PrimaryBackgroundOpacity { get; set; } = 0.55;
    public float PrimaryAnchorX { get; set; } = 0.475f;
    public float PrimaryAnchorY { get; set; } = 0.475f;

    public bool GenerateLandscape { get; set; } = true;
    public bool OverwriteExistingGeneratedLandscape { get; set; } = true;
    public string LandscapeOverlayPosition { get; set; } = "top-left";
    public int LandscapeFlagWidthPercent { get; set; } = 12;
    public int LandscapeMarginPercent { get; set; } = 2;
    public double LandscapeBackgroundOpacity { get; set; } = 0.55;
    public float LandscapeAnchorX { get; set; } = 0.485f;
    public float LandscapeAnchorY { get; set; } = 0.465f;
}