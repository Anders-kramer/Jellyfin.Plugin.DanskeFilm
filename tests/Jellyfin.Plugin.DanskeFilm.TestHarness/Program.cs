using System.Net.Http;
using Jellyfin.Plugin.DanskeFilm.Services;

if (args.Length == 0)
{
    Console.WriteLine("Brug:");
    Console.WriteLine("  dotnet run -- search \"Olsen Banden\"");
    Console.WriteLine("  dotnet run -- movie 123");
    return;
}

var command = args[0].Trim().ToLowerInvariant();

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.Plugin.DanskeFilm.TestHarness/0.1");

var client = new DanskeFilmClient(httpClient);
var parser = new DanskeFilmParser();

switch (command)
{
    case "search":
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Mangler søgetekst.");
            return;
        }

        var query = string.Join(' ', args.Skip(1));
        var html = await client.SearchAsync(query, CancellationToken.None);
        var results = parser.ParseSearchResults(html);

        Console.WriteLine($"Søger efter: {query}");
        Console.WriteLine();

        var i = 1;
        foreach (var result in results.Take(20))
        {
            Console.WriteLine($"{i}. {result.Title} | år: {result.Year?.ToString() ?? "ukendt"} | id: {result.Id}");
            Console.WriteLine($"   url: {result.Url}");
            i++;
        }

        if (!results.Any())
        {
            Console.WriteLine("Ingen resultater fundet.");
        }

        break;
    }

    case "movie":
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Mangler film-id.");
            return;
        }

        var id = args[1].Trim();
        var html = await client.GetMoviePageAsync(id, CancellationToken.None);
        var data = parser.ParseMoviePage(html, $"https://www.danskefilm.dk/film.php?id={id}");

        Console.WriteLine($"ID:           {id}");
        Console.WriteLine($"Titel:        {data.Title}");
        Console.WriteLine($"År:           {data.Year}");
        Console.WriteLine($"Instruktør:   {data.Director}");
        Console.WriteLine($"Poster:       {data.PosterUrl}");
        Console.WriteLine();

        Console.WriteLine("Plot:");
        Console.WriteLine(data.Overview);
        Console.WriteLine();

        Console.WriteLine("Cast:");
        foreach (var actor in data.Cast.Take(30))
        {
            Console.WriteLine($"- {actor}");
        }

        Console.WriteLine();
        Console.WriteLine("Billeder:");
        foreach (var image in data.ImageUrls.Take(20))
        {
            Console.WriteLine($"- {image}");
        }

        break;
    }

    default:
        Console.WriteLine($"Ukendt kommando: {command}");
        Console.WriteLine("Brug 'search' eller 'movie'.");
        break;
}
