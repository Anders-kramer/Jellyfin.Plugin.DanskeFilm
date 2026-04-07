using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.ExternalIds;

public class DanskeFilmExternalId : IExternalId
{
    public string Name => "DanskeFilm";

    public string Key => "DanskeFilm";

    public string ProviderName => "DanskeFilm";

    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    public string UrlFormatString => "https://www.danskefilm.dk/film.php?id={0}";

    public bool Supports(IHasProviderIds item)
    {
        return item is Video;
    }
}
