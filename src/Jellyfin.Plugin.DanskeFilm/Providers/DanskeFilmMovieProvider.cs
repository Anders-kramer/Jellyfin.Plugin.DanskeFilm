using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DanskeFilm.Models;
using Jellyfin.Plugin.DanskeFilm.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DanskeFilm.Providers;

public class DanskeFilmMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly HttpClient _httpClient;
    private readonly DanskeFilmClient _client;
    private readonly DanskeFilmParser _parser;

    public DanskeFilmMovieProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();
    }

    public string Name => "DanskeFilm";

    public int Order => 0;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        var title = searchInfo.Name?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        var html = await _client.SearchAsync(title, cancellationToken).ConfigureAwait(false);
        var parsed = _parser.ParseSearchResults(html);

        var results = parsed.Select(x => new RemoteSearchResult
        {
            Name = x.Title,
            ProductionYear = x.Year,
            SearchProviderName = Name,
            ProviderIds = new Dictionary<string, string>
            {
                { "DanskeFilm", x.Id }
            }
        });

        return results;
    }

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie>
        {
            Item = new Movie()
        };

        var movie = result.Item;
        movie.Name = info.Name;

        var danskefilmId = GetDanskeFilmId(info);

        if (string.IsNullOrWhiteSpace(danskefilmId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            var searchHtml = await _client.SearchAsync(info.Name, cancellationToken).ConfigureAwait(false);
            var searchResults = _parser.ParseSearchResults(searchHtml);
            var bestMatch = searchResults.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(bestMatch.Id))
            {
                danskefilmId = bestMatch.Id;
            }
        }

        if (string.IsNullOrWhiteSpace(danskefilmId))
        {
            result.HasMetadata = false;
            return result;
        }

        var pageHtml = await _client.GetMoviePageAsync(danskefilmId, cancellationToken).ConfigureAwait(false);
        var data = _parser.ParseMoviePage(pageHtml, $"https://www.danskefilm.dk/film.php?id={danskefilmId}");

        ApplyData(result, data, danskefilmId);

        result.HasMetadata = true;
        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(url, cancellationToken);
    }

    private static string? GetDanskeFilmId(MovieInfo info)
    {
        if (info.ProviderIds is not null &&
            info.ProviderIds.TryGetValue("DanskeFilm", out var id) &&
            !string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return null;
    }

    private static void ApplyData(MetadataResult<Movie> result, DanskeFilmMovieData data, string danskefilmId)
    {
        var movie = result.Item;
        if (movie is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.Title))
        {
            movie.Name = data.Title;
        }

        if (data.Year.HasValue)
        {
            movie.ProductionYear = data.Year.Value;
        }

        if (!string.IsNullOrWhiteSpace(data.Overview))
        {
            movie.Overview = data.Overview;
        }

        foreach (var genre in data.Genres.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            movie.AddGenre(genre);
        }

        movie.ProviderIds["DanskeFilm"] = danskefilmId;

        if (!string.IsNullOrWhiteSpace(data.Director))
        {
            result.AddPerson(new PersonInfo
            {
                Name = data.Director.Trim(),
                Type = PersonKind.Director
            });
        }

        foreach (var writer in data.Writers.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            result.AddPerson(new PersonInfo
            {
                Name = writer.Trim(),
                Type = PersonKind.Writer
            });
        }

        foreach (var actor in data.Cast.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            var person = new PersonInfo
            {
                Name = actor.Name.Trim(),
                Role = string.IsNullOrWhiteSpace(actor.Role) ? null : actor.Role.Trim(),
                Type = PersonKind.Actor
            };

            if (!string.IsNullOrWhiteSpace(actor.PersonId))
            {
                person.ProviderIds = new Dictionary<string, string>
                {
                    { "DanskeFilm", actor.PersonId! }
                };
            }

            result.AddPerson(person);
        }
    }
}
