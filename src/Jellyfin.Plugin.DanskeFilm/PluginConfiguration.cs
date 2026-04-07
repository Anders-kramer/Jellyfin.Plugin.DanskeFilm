using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DanskeFilm;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool EnableMovieMetadata { get; set; } = true;

    public bool EnableMovieImages { get; set; } = true;

    public string BaseUrl { get; set; } = "https://www.danskefilm.dk/";
}
