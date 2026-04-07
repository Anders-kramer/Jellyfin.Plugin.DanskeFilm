using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.ExternalIds;

public class DanskeFilmPersonExternalId : IExternalId
{
    public string Name => "DanskeFilm Person";

    public string Key => "DanskeFilm";

    public string ProviderName => "DanskeFilm";

    public ExternalIdMediaType? Type => ExternalIdMediaType.Person;

    public string UrlFormatString => "https://www.danskefilm.dk/skuespiller.php?id={0}";

    public bool Supports(IHasProviderIds item)
    {
        return item is Person;
    }
}
