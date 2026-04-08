using System;
using System.Collections.Generic;
using System.IO;
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

    private const string DebugLog = "/config/plugins/Jellyfin.Plugin.DanskeFilm/debug.log";

    public DanskeFilmMovieImageProvider()
    {
        Log("MOVIE IMAGE PROVIDER CTOR called");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm/0.1");
        _client = new DanskeFilmClient(_httpClient);
        _parser = new DanskeFilmParser();
    }

    public string Name => "DanskeFilm Movie Images";

    public int Order => 0;

    public bool Supports(BaseItem item)
    {
        var supported = item is Movie;
        Log($"MovieImageProvider.Supports type='{item.GetType().Name}' result='{supported}'");
        return supported;
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
            Log("MovieImageProvider.GetImages EXIT item is not Movie");
            return [];
        }

        Log($"MovieImageProvider.GetImages ENTER item='{movie.Name}' type='{movie.GetType().Name}'");

        if (movie.ProviderIds is null ||
            !movie.ProviderIds.TryGetValue("DanskeFilm", out var id) ||
            string.IsNullOrWhiteSpace(id))
        {
            Log("MovieImageProvider.GetImages EXIT missing DanskeFilm provider id");
            return [];
        }

        Log($"MovieImageProvider.GetImages provider_id='{id}'");

        var html = await _client.GetMoviePageAsync(id, cancellationToken).ConfigureAwait(false);
        Log($"MovieImageProvider.GetImages html_length={html.Length}");

        var data = _parser.ParseMoviePage(html, $"https://www.danskefilm.dk/film.php?id={id}");
        Log($"MovieImageProvider.GetImages parsed poster='{data.PosterUrl}' image_count='{data.ImageUrls.Count}'");

        foreach (var parsed in data.ImageUrls)
        {
            Log($"MovieImageProvider.GetImages parsed image='{parsed}'");
        }

        var results = new List<RemoteImageInfo>();

        var posterUrl = string.IsNullOrWhiteSpace(data.PosterUrl)
            ? null
            : data.PosterUrl.Trim();

        var allImages = data.ImageUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var backdropCandidates = allImages
            .Where(IsLargeImageCandidate)
            .Where(x => posterUrl is null || !string.Equals(x, posterUrl, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(posterUrl))
        {
            results.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = posterUrl,
                Type = ImageType.Primary
            });

            Log($"MovieImageProvider.GetImages add PRIMARY poster='{posterUrl}'");
        }
        else
        {
            Log("MovieImageProvider.GetImages no poster found for PRIMARY");
        }

        foreach (var url in backdropCandidates)
        {
            results.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = url,
                Type = ImageType.Backdrop
            });

            Log($"MovieImageProvider.GetImages add BACKDROP '{url}'");
        }

        Log($"MovieImageProvider.GetImages EXIT result_count='{results.Count}'");
        return results;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        Log($"MovieImageProvider.GetImageResponse url='{url}'");
        return _httpClient.GetAsync(url, cancellationToken);
    }

    private static bool IsLargeImageCandidate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var u = url.ToLowerInvariant();

        if (u.EndsWith("l.jpg"))
        {
            return false;
        }

        return u.EndsWith("ass.jpg") ||
               u.EndsWith("bss.jpg") ||
               u.EndsWith("css.jpg") ||
               u.EndsWith("ds.jpg") ||
               u.EndsWith("ess.jpg") ||
               u.EndsWith("fs.jpg") ||
               u.EndsWith("gs.jpg") ||
               u.EndsWith("hs.jpg") ||
               u.EndsWith("is.jpg");
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
