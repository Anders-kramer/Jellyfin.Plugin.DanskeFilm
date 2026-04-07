using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Jellyfin.Plugin.DanskeFilm.Models;

namespace Jellyfin.Plugin.DanskeFilm.Services;

public class DanskeFilmParser
{
    public List<(string Id, string Title, int? Year, string Url)> ParseSearchResults(string html)
    {
        var doc = Load(html);
        var results = new List<(string Id, string Title, int? Year, string Url)>();

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, 'film.php?id=')]");
        if (links is null)
        {
            return results;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            var id = ExtractFilmId(href);
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            var title = Clean(link.InnerText);
            var year = TryParseYear(title);
            var absoluteUrl = ToAbsoluteUrl(href);

            results.Add((id, title, year, absoluteUrl));
        }

        return results;
    }

    public DanskeFilmMovieData ParseMoviePage(string html, string sourceUrl)
    {
        var doc = Load(html);

        var data = new DanskeFilmMovieData
        {
            SourceUrl = sourceUrl,
            Title = ParseTitle(doc),
            Year = ParseYear(doc),
            Overview = ParseOverview(doc),
            Director = ParseDirector(doc),
            Cast = ParseCast(doc),
            PosterUrl = ParsePosterUrl(doc),
            ImageUrls = ParseImageUrls(doc)
        };

        return data;
    }

    private static HtmlDocument Load(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    private static string? ParseTitle(HtmlDocument doc)
    {
        var h1 = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1 is not null)
        {
            return Clean(h1.InnerText);
        }

        var title = doc.DocumentNode.SelectSingleNode("//title");
        return title is null ? null : Clean(title.InnerText);
    }

    private static int? ParseYear(HtmlDocument doc)
    {
        var text = Clean(doc.DocumentNode.InnerText);
        return TryParseYear(text);
    }

    private static string? ParseOverview(HtmlDocument doc)
    {
        var candidates = doc.DocumentNode.SelectNodes("//p|//td|//div");
        if (candidates is null)
        {
            return null;
        }

        foreach (var node in candidates)
        {
            var text = Clean(node.InnerText);
            if (text.Length < 100)
            {
                continue;
            }

            if (text.Contains("Medvirkende", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private static string? ParseDirector(HtmlDocument doc)
    {
        var text = Clean(doc.DocumentNode.InnerText);
        var match = Regex.Match(text, @"Instruktør\s*:?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return Clean(match.Groups[1].Value.Split('\n')[0]);
    }

    private static List<string> ParseCast(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'skuespiller.php?id=')]");
        if (nodes is null)
        {
            return [];
        }

        return nodes
            .Select(x => Clean(x.InnerText))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ParsePosterUrl(HtmlDocument doc)
    {
        var img = doc.DocumentNode.SelectSingleNode("//img[contains(@src, '/data/')]")
               ?? doc.DocumentNode.SelectSingleNode("//img");

        if (img is null)
        {
            return null;
        }

        var src = img.GetAttributeValue("src", string.Empty);
        return string.IsNullOrWhiteSpace(src) ? null : ToAbsoluteUrl(src);
    }

    private static List<string> ParseImageUrls(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//img");
        if (nodes is null)
        {
            return [];
        }

        return nodes
            .Select(x => x.GetAttributeValue("src", string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(ToAbsoluteUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractFilmId(string href)
    {
        var match = Regex.Match(href, @"[?&]id=(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static int? TryParseYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"\b(19\d{2}|20\d{2})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var year) ? year : null;
    }

    private static string ToAbsoluteUrl(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return $"https://www.danskefilm.dk/{url.TrimStart('/')}";
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(text);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
