using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.ExternalIds;

public class DanskeFilmMovieExternalId : IExternalId
{
    public string Name => "DanskeFilm Movie Id";

    public string Key => "DanskeFilm";

    public string? UrlFormatString => "https://www.danskefilm.dk/film.php?id={0}";

    public string ProviderName => "DanskeFilm";

    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    public bool Supports(IHasProviderIds item)
    {
        return item is Movie;
    }
}
