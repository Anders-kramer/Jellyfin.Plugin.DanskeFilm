using System.Globalization;
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
            var id = ExtractIdFromUrl(href);
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
        var filmId = ExtractIdFromUrl(sourceUrl);

        return new DanskeFilmMovieData
        {
            SourceUrl = sourceUrl,
            Id = filmId,
            Title = ParseTitle(doc),
            Year = ParseYear(doc),
            Overview = ParseOverview(doc),
            Director = ParseDirector(doc),
            Cast = ParseCast(doc),
            Writers = ParseWriters(doc),
            Studios = ParseStudios(doc),
            Genres = ParseGenres(doc),
            PosterUrl = ParsePosterUrl(doc, html, filmId),
            ImageUrls = ParseImageUrls(doc, html, filmId),
            PremiereDate = ParsePremiereDate(doc),
            RuntimeMinutes = ParseRuntimeMinutes(doc),
            TrailerUrl = ParseTrailerUrl(doc)
        };
    }

    public DanskeFilmPersonData ParsePersonPage(string html, string sourceUrl)
    {
        var doc = Load(html);
        var personId = ExtractIdFromUrl(sourceUrl);

        return new DanskeFilmPersonData
        {
            Id = personId,
            SourceUrl = sourceUrl,
            Name = ParsePersonName(doc),
            Biography = ParseBiography(doc),
            BirthDate = ParseBirthDate(doc),
            BirthPlace = ParseBirthPlace(doc),
            DeathDate = ParseDeathDate(doc),
            GraveSite = ParseGraveSite(doc),
            ImageUrls = ParsePersonImages(doc, personId),
            Filmography = ParseFilmography(doc)
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
        var titleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//h4[contains(@class,'media-heading')]//b");
        if (titleNode is not null)
        {
            return Clean(titleNode.InnerText);
        }

        var fallback = doc.DocumentNode.SelectSingleNode("//title");
        return fallback is null ? null : StripYearFromTitle(Clean(fallback.InnerText));
    }

    private static int? ParseYear(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode is not null)
        {
            var year = TryParseYear(Clean(titleNode.InnerText));
            if (year.HasValue)
            {
                return year;
            }
        }

        var mediaHeading = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//h4");
        return TryParseYear(Clean(mediaHeading?.InnerText));
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
        var creditsRows = GetCreditsRows(doc);
        foreach (var row in creditsRows)
        {
            if (row.Label.Equals("Instruktion", StringComparison.OrdinalIgnoreCase) ||
                row.Label.Equals("Instruktør", StringComparison.OrdinalIgnoreCase))
            {
                return row.Value;
            }
        }

        return null;
    }

    private static List<string> ParseWriters(HtmlDocument doc)
    {
        return GetCreditsRows(doc)
            .Where(x => x.Label.Equals("Manuskript", StringComparison.OrdinalIgnoreCase) ||
                        x.Label.Equals("Drejebog", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseStudios(HtmlDocument doc)
    {
        return GetCreditsRows(doc)
            .Where(x => x.Label.Equals("Produktionsselskab", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseGenres(HtmlDocument doc)
    {
        var result = new List<string>();

        var smallNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//small");
        var text = Clean(smallNode?.InnerText);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var firstPart = text.Split('.').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstPart))
            {
                result.Add(firstPart.Trim());
            }
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DanskeFilmCastMember> ParseCast(HtmlDocument doc)
    {
        var castTable = GetSectionTable(doc, "Medvirkende");
        if (castTable is null)
        {
            return [];
        }

        var rows = castTable.SelectNodes(".//tbody/tr");
        if (rows is null)
        {
            return [];
        }

        var result = new List<DanskeFilmCastMember>();

        foreach (var row in rows)
        {
            var nameLink = row.SelectSingleNode("./td[1]//a[contains(@href, 'skuespiller.php?id=')]");
            if (nameLink is null)
            {
                continue;
            }

            var name = Clean(nameLink.InnerText);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var roleCell = row.SelectSingleNode("./td[2]");
            var role = Clean(roleCell?.InnerText);
            if (string.IsNullOrWhiteSpace(role))
            {
                role = null;
            }

            var href = nameLink.GetAttributeValue("href", string.Empty);
            var profileUrl = string.IsNullOrWhiteSpace(href) ? null : ToAbsoluteUrl(href);
            var personId = string.IsNullOrWhiteSpace(href) ? null : ExtractIdFromUrl(href);

            result.Add(new DanskeFilmCastMember
            {
                Name = name,
                Role = role,
                ProfileUrl = profileUrl,
                PersonId = personId
            });
        }

        return result
            .GroupBy(x => $"{x.Name}|||{x.Role}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static HtmlNode? GetSectionTable(HtmlDocument doc, string sectionName)
    {
        return doc.DocumentNode.SelectSingleNode(
            $"//table[.//thead//b[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ', 'abcdefghijklmnopqrstuvwxyzæøå'), '{sectionName.ToLowerInvariant()}')]]");
    }

    private static List<(string Label, string Value)> GetCreditsRows(HtmlDocument doc)
    {
        var result = new List<(string Label, string Value)>();
        var creditsTable = GetSectionTable(doc, "Kredits");
        if (creditsTable is null)
        {
            return result;
        }

        var rows = creditsTable.SelectNodes(".//tbody/tr");
        if (rows is null)
        {
            return result;
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

            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
            {
                result.Add((label, value));
            }
        }

        return result;
    }

    private static string? ParsePremiereDate(HtmlDocument doc)
    {
        var smallNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//small");
        var text = Clean(smallNode?.InnerText);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"Premiere:\s*(\d{1,2}/\d{1,2}-\d{4})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        if (DateTime.TryParseExact(match.Groups[1].Value, "d/M-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParseExact(match.Groups[1].Value, "dd/MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static int? ParseRuntimeMinutes(HtmlDocument doc)
    {
        var smallNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-body')]//small");
        var text = Clean(smallNode?.InnerText);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"(\d+)\s*min\.", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var minutes) ? minutes : null;
    }

    private static string? ParseTrailerUrl(HtmlDocument doc)
    {
        var sourceNode = doc.DocumentNode.SelectSingleNode("//video/source[@src]");
        var src = sourceNode?.GetAttributeValue("src", null);
        return NormalizeImageUrl(src);
    }

    private static string? ParsePosterUrl(HtmlDocument doc, string html, string filmId)
    {
        var posterNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'media-left')]//img[contains(@class,'media-object')]");
        var posterSrc = posterNode?.GetAttributeValue("src", null);

        if (!string.IsNullOrWhiteSpace(posterSrc))
        {
            return NormalizeImageUrl(posterSrc);
        }

        if (!string.IsNullOrWhiteSpace(filmId))
        {
            return $"https://danskefilm.dk/film_billeder/{filmId}l.jpg";
        }

        return null;
    }

    private static List<string> ParseImageUrls(HtmlDocument doc, string html, string filmId)
    {
        var results = new List<string>();

        var matches = Regex.Matches(
            html,
            "src:\\s*[\"'](?<url>(?://|https?://)[^\"']+/film_billeder/[^\"']+)[\"']",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value;
            var normalized = NormalizeImageUrl(url);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                results.Add(normalized);
            }
        }

        if (!string.IsNullOrWhiteSpace(filmId))
        {
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}asx.jpg");
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}bsx.jpg");
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}csx.jpg");
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}ass.jpg");
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}bss.jpg");
            results.Add($"https://danskefilm.dk/film_billeder/{filmId}css.jpg");
        }

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = WebUtility.HtmlDecode(url.Trim());

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return "https://danskefilm.dk" + url;
        }

        return url;
    }

    private static List<string> ParsePersonImages(HtmlDocument doc, string? personId)
    {
        var result = new List<string>();

        var imageNodes = doc.DocumentNode.SelectNodes("//img[contains(@src, '/data/images/') or contains(@src, '/bilder/') or contains(@src, '/billeder/')]");
        if (imageNodes is not null)
        {
            foreach (var imageNode in imageNodes)
            {
                var src = imageNode.GetAttributeValue("src", null);
                var normalized = NormalizeImageUrl(src);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DanskeFilmFilmographyEntry> ParseFilmography(HtmlDocument doc)
    {
        return [];
    }

    private static string? ParsePersonName(HtmlDocument doc)
    {
        var title = doc.DocumentNode.SelectSingleNode("//title");
        return title is null ? null : Clean(title.InnerText);
    }

    private static string? ParseBiography(HtmlDocument doc)
    {
        return null;
    }

    private static string? ParseBirthDate(HtmlDocument doc)
    {
        return null;
    }

    private static string? ParseBirthPlace(HtmlDocument doc)
    {
        return null;
    }

    private static string? ParseDeathDate(HtmlDocument doc)
    {
        return null;
    }

    private static string? ParseGraveSite(HtmlDocument doc)
    {
        return null;
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(text);
        decoded = Regex.Replace(decoded, @"\s+", " ").Trim();
        return decoded;
    }

    private static int? TryParseYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"\b(18|19|20)\d{2}\b");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Value, out var year) ? year : null;
    }

    private static string StripYearFromTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Regex.Replace(text, @"\s*\((18|19|20)\d{2}\)\s*$", "").Trim();
    }

    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = Regex.Match(url, @"id=(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ToAbsoluteUrl(string url)
    {
        var normalized = NormalizeImageUrl(url);
        return normalized ?? url;
    }
}
