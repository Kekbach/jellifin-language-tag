using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using Jellyfin.Plugin.LanguageFlags.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.LanguageFlags;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        var pluginDir = Path.GetDirectoryName(GetType().Assembly.Location);
        if (!string.IsNullOrWhiteSpace(pluginDir))
        {
            Preload(pluginDir, "SixLabors.ImageSharp.dll");
            Preload(pluginDir, "SixLabors.Fonts.dll");
            Preload(pluginDir, "SixLabors.ImageSharp.Drawing.dll");
        }
    }

    public override string Name => "Language Flags Overlay";

    public override Guid Id => Guid.Parse("3d4c6d2f-3a93-4d67-8d22-3a9c5a7e5b91");

    public override string Description => "Adds language flag overlays to poster images for movies and series.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "configPage.html",
                EmbeddedResourcePath = "Jellyfin.Plugin.LanguageFlags.Configuration.configPage.html"
            }
        };
    }

    private static void Preload(string pluginDir, string fileName)
    {
        var fullPath = Path.Combine(pluginDir, fileName);
        if (File.Exists(fullPath))
        {
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }
}