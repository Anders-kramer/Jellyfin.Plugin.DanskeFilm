using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DanskeFilm.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.Providers;

public class DanskeFilmPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
{
    private readonly HttpClient _httpClient;
    private readonly DanskeFilmClient _client;
    private readonly DanskeFilmParser _parser;

    public DanskeFilmPersonProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();
    }

    public string Name => "DanskeFilm Person";

    public int Order => 0;

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
    {
        IEnumerable<RemoteSearchResult> results = [];
        return Task.FromResult(results);
    }

    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Person>
        {
            Item = new Person()
        };

        var person = result.Item;
        person.Name = info.Name;

        string? personId = null;

        if (info.ProviderIds is not null &&
            info.ProviderIds.TryGetValue("DanskeFilm", out var existingId) &&
            !string.IsNullOrWhiteSpace(existingId))
        {
            personId = existingId;
        }

        if (string.IsNullOrWhiteSpace(personId))
        {
            result.HasMetadata = false;
            return result;
        }

        var html = await _client.GetPersonPageAsync(personId, cancellationToken).ConfigureAwait(false);
        var data = _parser.ParsePersonPage(html, $"https://www.danskefilm.dk/skuespiller.php?id={personId}");

        if (!string.IsNullOrWhiteSpace(data.Name))
        {
            person.Name = data.Name;
        }

        if (!string.IsNullOrWhiteSpace(data.Biography))
        {
            person.Overview = data.Biography;
        }

        if (!string.IsNullOrWhiteSpace(data.BirthDate) &&
            System.DateTime.TryParse(data.BirthDate, out var birth))
        {
            person.PremiereDate = birth;
        }

        if (!string.IsNullOrWhiteSpace(personId))
        {
            person.ProviderIds["DanskeFilm"] = personId;
        }

        result.HasMetadata = true;
        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(url, cancellationToken);
    }
}
