using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DanskeFilm.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.Providers;

public class DanskeFilmMovieImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly HttpClient _httpClient;
    private readonly DanskeFilmClient _client;
    private readonly DanskeFilmParser _parser;

    public DanskeFilmMovieImageProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();
    }

    public string Name => "DanskeFilm Movie Images";

    public int Order => 0;

    public bool Supports(BaseItem item)
    {
        return item is Movie;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        if (!Supports(item))
        {
            return [];
        }

        return
        [
            ImageType.Primary,
            ImageType.Backdrop
        ];
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not Movie movie)
        {
            return [];
        }

        if (movie.ProviderIds is null ||
            !movie.ProviderIds.TryGetValue("DanskeFilm", out var id) ||
            string.IsNullOrWhiteSpace(id))
        {
            return [];
        }

        var html = await _client.GetMoviePageAsync(id, cancellationToken).ConfigureAwait(false);
        var data = _parser.ParseMoviePage(html, $"https://www.danskefilm.dk/film.php?id={id}");

        var results = new List<RemoteImageInfo>();

        for (var i = 0; i < data.ImageUrls.Count; i++)
        {
            var url = data.ImageUrls[i];
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            results.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = url,
                Type = i == 0 ? ImageType.Primary : ImageType.Backdrop
            });
        }

        return results;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(url, cancellationToken);
    }
}
