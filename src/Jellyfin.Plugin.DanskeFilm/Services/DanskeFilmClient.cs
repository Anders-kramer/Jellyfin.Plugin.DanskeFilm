using System.Text;

namespace Jellyfin.Plugin.DanskeFilm.Services;

public class DanskeFilmClient
{
    private readonly HttpClient _httpClient;

    public DanskeFilmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var latin1 = Encoding.GetEncoding("ISO-8859-1");
        return latin1.GetString(bytes);
    }

    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"https://www.danskefilm.dk/search.php?q={Uri.EscapeDataString(query)}";
        return await GetStringAsync(url, cancellationToken);
    }

    public async Task<string> GetMoviePageAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"https://www.danskefilm.dk/film.php?id={Uri.EscapeDataString(id)}";
        return await GetStringAsync(url, cancellationToken);
    }
}
