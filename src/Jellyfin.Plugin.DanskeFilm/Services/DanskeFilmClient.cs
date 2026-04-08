using System;
using System.IO;
using System.Text;

namespace Jellyfin.Plugin.DanskeFilm.Services;

public class DanskeFilmClient
{
    private readonly HttpClient _httpClient;
    private const string DebugLog = "/config/plugins/Jellyfin.Plugin.DanskeFilm/debug.log";

    public DanskeFilmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Log("CTOR DanskeFilmClient created");
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        Log($"HTTP GET start url='{url}'");

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        Log($"HTTP GET status={(int)response.StatusCode} url='{url}'");

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        Log($"HTTP GET bytes={bytes.Length} url='{url}'");

        var latin1 = Encoding.GetEncoding("ISO-8859-1");
        var text = latin1.GetString(bytes);
        Log($"HTTP GET decoded_chars={text.Length} url='{url}'");

        return text;
    }

    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"https://www.danskefilm.dk/search.php?q={Uri.EscapeDataString(query)}";
        Log($"SearchAsync query='{query}'");
        return await GetStringAsync(url, cancellationToken);
    }

    public async Task<string> GetMoviePageAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"https://www.danskefilm.dk/film.php?id={Uri.EscapeDataString(id)}";
        Log($"GetMoviePageAsync id='{id}'");
        return await GetStringAsync(url, cancellationToken);
    }

    public async Task<string> GetPersonPageAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"https://www.danskefilm.dk/skuespiller.php?id={Uri.EscapeDataString(id)}";
        Log($"GetPersonPageAsync id='{id}'");
        return await GetStringAsync(url, cancellationToken);
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
