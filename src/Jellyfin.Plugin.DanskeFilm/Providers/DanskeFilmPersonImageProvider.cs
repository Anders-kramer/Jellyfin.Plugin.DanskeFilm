using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DanskeFilm.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.Providers;

public class DanskeFilmPersonImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly HttpClient _httpClient;
    private readonly DanskeFilmClient _client;
    private readonly DanskeFilmParser _parser;

    public DanskeFilmPersonImageProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();
    }

    public string Name => "DanskeFilm Person Images";

    public int Order => 0;

    public bool Supports(BaseItem item)
    {
        return item is Person;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        if (!Supports(item))
        {
            return [];
        }

        return
        [
            ImageType.Primary
        ];
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not Person person)
        {
            return [];
        }

        if (person.ProviderIds is null ||
            !person.ProviderIds.TryGetValue("DanskeFilm", out var id) ||
            string.IsNullOrWhiteSpace(id))
        {
            return [];
        }

        var html = await _client.GetPersonPageAsync(id, cancellationToken).ConfigureAwait(false);
        var data = _parser.ParsePersonPage(html, $"https://www.danskefilm.dk/skuespiller.php?id={id}");

        var results = new List<RemoteImageInfo>();

        foreach (var url in data.ImageUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            results.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = url,
                Type = ImageType.Primary
            });
        }

        return results;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(url, cancellationToken);
    }
}
