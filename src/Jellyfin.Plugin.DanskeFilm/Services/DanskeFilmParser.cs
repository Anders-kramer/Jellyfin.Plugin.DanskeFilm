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

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, 'film.php?id=') or contains(@href, 'tegnefilm.php?id=')]");
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

            var rawTitle = Clean(link.InnerText);
            var year = TryParseYear(rawTitle);
            var title = StripYearFromTitle(rawTitle);
            var absoluteUrl = ToAbsoluteUrl(href);

            results.Add((id, title, year, absoluteUrl));
        }

        return results;
    }

    public DanskeFilmMovieData ParseMoviePage(string html, string sourceUrl)
    {
        var doc = Load(html);
        var filmId = ExtractFilmId(sourceUrl);

        return new DanskeFilmMovieData
        {
            SourceUrl = sourceUrl,
            Id = filmId,
            Title = ParseTitle(doc),
            Year = ParseYear(doc),
            Overview = ParseOverview(doc),
            Director = ParseDirector(doc),
            Cast = ParseCast(doc),
            PosterUrl = ParsePosterUrl(doc, html, filmId),
            ImageUrls = ParseImageUrls(doc, html, filmId)
        };
    }

    private static HtmlDocument Load(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    private static string? ParseTitle(HtmlDocument doc)
    {
        var h1 = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//h4[contains(@class,'media-heading')]//b");
        if (h1 is not null)
        {
            return Clean(h1.InnerText);
        }

        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode is not null)
        {
            return StripYearFromTitle(Clean(titleNode.InnerText));
        }

        return null;
    }

    private static int? ParseYear(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode is not null)
        {
            var fromTitle = TryParseYear(Clean(titleNode.InnerText));
            if (fromTitle.HasValue)
            {
                return fromTitle;
            }
        }

        var headingText = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//h4")?.InnerText;
        return TryParseYear(Clean(headingText));
    }

    private static string? ParseOverview(HtmlDocument doc)
    {
        var descriptionDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'beskrivelse')]");
        if (descriptionDiv is null)
        {
            return null;
        }

        var text = Clean(descriptionDiv.InnerText);

        text = Regex.Replace(text, @"^\s*Beskrivelse\s*", "", RegexOptions.IgnoreCase).Trim();
        text = Regex.Replace(text, @"\s*vis mere\s*$", "", RegexOptions.IgnoreCase).Trim();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ParseDirector(HtmlDocument doc)
    {
        var creditsTable = GetSectionTable(doc, "Kredits");
        if (creditsTable is null)
        {
            return null;
        }

        var rows = creditsTable.SelectNodes(".//tr");
        if (rows is null)
        {
            return null;
        }

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 2)
            {
                continue;
            }

            var label = Clean(cells[0].InnerText);
            var value = Clean(cells[1].InnerText);

            if (label.Equals("Instruktion", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("Instruktør", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private static List<string> ParseCast(HtmlDocument doc)
    {
        var castTable = GetSectionTable(doc, "Medvirkende");
        if (castTable is null)
        {
            return [];
        }

        var actorLinks = castTable.SelectNodes(".//tbody//a[contains(@href, 'skuespiller.php?id=')]");
        if (actorLinks is null)
        {
            return [];
        }

        return actorLinks
            .Select(x => Clean(x.InnerText))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlNode? GetSectionTable(HtmlDocument doc, string sectionName)
    {
        return doc.DocumentNode.SelectSingleNode(
            $"//table[.//thead//b[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ', 'abcdefghijklmnopqrstuvwxyzæøå'), '{sectionName.ToLowerInvariant()}')]]");
    }

    private static string? ParsePosterUrl(HtmlDocument doc, string html, string filmId)
    {
        var images = ParseImageUrls(doc, html, filmId);
        return images.FirstOrDefault();
    }

    private static List<string> ParseImageUrls(HtmlDocument doc, string html, string filmId)
    {
        var results = new List<string>();

        // 1. bedst: parse popup-gallery script med store billeder
        var popupPattern = $@"src:\s*[""'](?<url>//danskefilm\.dk/film_billeder/{Regex.Escape(filmId)}[a-z]*sn\.jpg)[""']";
        foreach (Match match in Regex.Matches(html, popupPattern, RegexOptions.IgnoreCase))
        {
            var url = match.Groups["url"].Value;
            if (!string.IsNullOrWhiteSpace(url))
            {
                results.Add(ToAbsoluteUrl(url));
            }
        }

        // 2. fallback: thumbnails på selve siden for samme film-id
        if (results.Count == 0)
        {
            var imageNodes = doc.DocumentNode.SelectNodes("//img[contains(@src, '/film_billeder/')]");
            if (imageNodes is not null)
            {
                foreach (var node in imageNodes)
                {
                    var src = node.GetAttributeValue("src", string.Empty);
                    var absolute = UpgradeImageUrl(ToAbsoluteUrl(src));

                    if (ImageBelongsToMovie(absolute, filmId))
                    {
                        results.Add(absolute);
                    }
                }
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ImageBelongsToMovie(string imageUrl, string filmId)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(filmId))
        {
            return false;
        }

        var fileName = Path.GetFileName(imageUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return Regex.IsMatch(
            fileName,
            $"^{Regex.Escape(filmId)}[a-z]*\\.(jpg|jpeg|png|webp)$",
            RegexOptions.IgnoreCase);
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

    private static string StripYearFromTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = Clean(text);
        cleaned = Regex.Replace(cleaned, @"\s*\((19\d{2}|20\d{2})\)\s*$", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static string ToAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("//"))
        {
            return "https:" + url;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeUrl(url);
        }

        return NormalizeUrl($"https://www.danskefilm.dk/{url.TrimStart('/')}");
    }

    private static string NormalizeUrl(string url)
    {
        return url.Replace("https://www.danskefilm.dk/danskefilm.dk/", "https://www.danskefilm.dk/")
                  .Replace("https://danskefilm.dk/danskefilm.dk/", "https://danskefilm.dk/")
                  .Replace("http://www.danskefilm.dk/danskefilm.dk/", "http://www.danskefilm.dk/")
                  .Replace("http://danskefilm.dk/danskefilm.dk/", "http://danskefilm.dk/");
    }

    private static string UpgradeImageUrl(string url)
    {
        url = NormalizeUrl(url);
        url = Regex.Replace(url, @"l\.jpg$", "asn.jpg", RegexOptions.IgnoreCase);
        return url;
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
