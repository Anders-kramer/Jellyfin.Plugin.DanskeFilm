using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.DanskeFilm;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private const string DebugLog = "/config/plugins/Jellyfin.Plugin.DanskeFilm/debug.log";

    public static Plugin? Instance { get; private set; }

    public override string Name => "DanskeFilm Metadata";

    public override Guid Id => new("5d0f9df8-9fc2-4a31-a4a0-6ad6fe7df301");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Log("PLUGIN CTOR called");
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        Log("PLUGIN GetPages called");
        return Array.Empty<PluginPageInfo>();
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory("/config/plugins/Jellyfin.Plugin.DanskeFilm");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(DebugLog, line);
        }
        catch
        {
        }
    }
}
