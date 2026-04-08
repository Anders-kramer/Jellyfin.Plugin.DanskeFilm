using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    private const string DebugLog = "/config/plugins/Jellyfin.Plugin.DanskeFilm/debug.log";

    public DanskeFilmMovieProvider()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();

        Log("CTOR MovieProvider created");
    }

    public string Name => "DanskeFilm";

    public int Order => 0;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        var providerIdsDebug = searchInfo?.ProviderIds is null
            ? "(null)"
            : string.Join(", ", searchInfo.ProviderIds.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        Log($"GetSearchResults ENTER name='{searchInfo?.Name}'");
        Log($"GetSearchResults provider_ids='{providerIdsDebug}'");

        if (searchInfo is not null &&
            searchInfo.ProviderIds is not null &&
            searchInfo.ProviderIds.TryGetValue("DanskeFilm", out var directDanskeFilmId) &&
            !string.IsNullOrWhiteSpace(directDanskeFilmId))
        {
            Log($"GetSearchResults direct id lookup id='{directDanskeFilmId}'");

            var directHtml = await _client.GetMoviePageAsync(directDanskeFilmId, cancellationToken).ConfigureAwait(false);
            Log($"GetSearchResults direct id html_length={directHtml.Length}");

            var directData = _parser.ParseMoviePage(directHtml, directDanskeFilmId);
            Log($"GetSearchResults direct id parsed title='{directData.Title}' year='{directData.Year}'");

            var directResult = new RemoteSearchResult
            {
                Name = directData.Title,
                ProductionYear = directData.Year,
                SearchProviderName = Name,
                Overview = directData.Overview,
                ImageUrl = directData.PosterUrl,
                ProviderIds = new Dictionary<string, string>
                {
                    { "DanskeFilm", directDanskeFilmId }
                }
            };

            Log("GetSearchResults EXIT direct id result_count=1");
            return new[] { directResult };
        }

        var title = searchInfo?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            Log("GetSearchResults EXIT empty title");
            return [];
        }

        var html = await _client.SearchAsync(title, cancellationToken).ConfigureAwait(false);
        Log($"GetSearchResults SearchAsync returned {html.Length} chars");

        var parsed = _parser.ParseSearchResults(html);
        Log($"GetSearchResults parser returned {parsed.Count} results");

        foreach (var hit in parsed.Take(10))
        {
            Log($"Search hit id='{hit.Id}' title='{hit.Title}' year='{hit.Year}' url='{hit.Url}'");
        }

        var results = parsed.Select(x => new RemoteSearchResult
        {
            Name = x.Title,
            ProductionYear = x.Year,
            SearchProviderName = Name,
            ProviderIds = new Dictionary<string, string>
            {
                { "DanskeFilm", x.Id }
            }
        }).ToList();

        Log($"GetSearchResults EXIT returning {results.Count} results");
        return results;
    }

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        Log($"GetMetadata ENTER name='{info?.Name}'");

        var result = new MetadataResult<Movie>
        {
            Item = new Movie()
        };

        var movie = result.Item;
        movie.Name = info.Name;

        var danskefilmId = GetDanskeFilmId(info);
        Log($"GetMetadata initial provider id='{danskefilmId}'");

        if (string.IsNullOrWhiteSpace(danskefilmId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            var searchHtml = await _client.SearchAsync(info.Name, cancellationToken).ConfigureAwait(false);
            Log($"GetMetadata SearchAsync fallback returned {searchHtml.Length} chars");

            var searchResults = _parser.ParseSearchResults(searchHtml);
            Log($"GetMetadata fallback parser returned {searchResults.Count} hits");

            var bestMatch = searchResults.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(bestMatch.Id))
            {
                danskefilmId = bestMatch.Id;
                Log($"GetMetadata fallback chose id='{danskefilmId}' title='{bestMatch.Title}'");
            }
        }

        if (string.IsNullOrWhiteSpace(danskefilmId))
        {
            Log("GetMetadata EXIT no id found");
            result.HasMetadata = false;
            return result;
        }

        var pageHtml = await _client.GetMoviePageAsync(danskefilmId, cancellationToken).ConfigureAwait(false);
        Log($"GetMetadata GetMoviePageAsync returned {pageHtml.Length} chars for id='{danskefilmId}'");

        var data = _parser.ParseMoviePage(pageHtml, $"https://www.danskefilm.dk/film.php?id={danskefilmId}");
        Log($"GetMetadata parsed title='{data.Title}' year='{data.Year}' overview_len='{data.Overview?.Length ?? 0}' cast='{data.Cast.Count}' writers='{data.Writers.Count}' genres='{data.Genres.Count}'");

        ApplyData(result, data, danskefilmId);

        result.HasMetadata = true;
        Log($"GetMetadata EXIT success movie.Name='{result.Item?.Name}' year='{result.Item?.ProductionYear}'");
        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        Log($"GetImageResponse url='{url}'");
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

        if (!string.IsNullOrWhiteSpace(data.PremiereDate))
        {
            var premiereText = data.PremiereDate.Trim();

            var premiereFormats = new[]
            {
                "d/M-yyyy",
                "dd/M-yyyy",
                "d/MM-yyyy",
                "dd/MM-yyyy",
                "yyyy-MM-dd"
            };

            if (DateTime.TryParseExact(
                premiereText,
                premiereFormats,
                CultureInfo.GetCultureInfo("da-DK"),
                DateTimeStyles.AssumeLocal,
                out var premiereDate))
            {
                movie.PremiereDate = premiereDate;
            }
        }

        if (data.RuntimeMinutes.HasValue && data.RuntimeMinutes.Value > 0)
        {
            movie.RunTimeTicks = data.RuntimeMinutes.Value * 600000000L;
        }

        foreach (var genre in data.Genres.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            movie.AddGenre(genre);
        }

        foreach (var studio in data.Studios.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            movie.AddStudio(studio);
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

        foreach (var credit in data.Credits.Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Role)))
        {
            var role = credit.Role.ToLowerInvariant();

            PersonKind? type = role switch
            {
                var r when r.Contains("instruktion") => PersonKind.Director,
                var r when r.Contains("producent") => PersonKind.Producer,
                var r when r.Contains("musik") => PersonKind.Composer,
                var r when r.Contains("drejebog") => PersonKind.Writer,
                _ => null
            };

            if (type is not null)
            {
                result.AddPerson(new PersonInfo
                {
                    Name = credit.Name.Trim(),
                    Type = type.Value
                });
            }
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

    private static void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(DebugLog, line);
        }
        catch
        {
        }
    }
}
