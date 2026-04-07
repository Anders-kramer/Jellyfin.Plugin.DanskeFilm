using System.Net.Http;
using Jellyfin.Plugin.DanskeFilm.Services;

if (args.Length == 0)
{
    Console.WriteLine("Brug:");
    Console.WriteLine("  dotnet run -- search \"Olsen Banden\"");
    Console.WriteLine("  dotnet run -- movie 265");
    Console.WriteLine("  dotnet run -- person 580");
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

        Console.WriteLine($"ID:             {id}");
        Console.WriteLine($"Titel:          {data.Title}");
        Console.WriteLine($"År:             {data.Year}");
        Console.WriteLine($"Premiere:       {data.PremiereDate}");
        Console.WriteLine($"Runtime:        {data.RuntimeMinutes}");
        Console.WriteLine($"Instruktør:     {data.Director}");
        Console.WriteLine($"Poster:         {data.PosterUrl}");
        Console.WriteLine($"Trailer:        {data.TrailerUrl}");
        Console.WriteLine();

        Console.WriteLine("Studios:");
        foreach (var studio in data.Studios)
        {
            Console.WriteLine($"- {studio}");
        }

        Console.WriteLine();
        Console.WriteLine("Writers:");
        foreach (var writer in data.Writers)
        {
            Console.WriteLine($"- {writer}");
        }

        Console.WriteLine();
        Console.WriteLine("Genres:");
        foreach (var genre in data.Genres)
        {
            Console.WriteLine($"- {genre}");
        }

        Console.WriteLine();
        Console.WriteLine("Plot:");
        Console.WriteLine(data.Overview);
        Console.WriteLine();

        Console.WriteLine("Cast:");
        foreach (var actor in data.Cast.Take(40))
        {
            var meta = "";
            if (!string.IsNullOrWhiteSpace(actor.PersonId))
            {
                meta = $" [person-id: {actor.PersonId}]";
            }

            if (string.IsNullOrWhiteSpace(actor.Role))
            {
                Console.WriteLine($"- {actor.Name}{meta}");
            }
            else
            {
                Console.WriteLine($"- {actor.Name} => {actor.Role}{meta}");
            }

            if (!string.IsNullOrWhiteSpace(actor.ProfileUrl))
            {
                Console.WriteLine($"    {actor.ProfileUrl}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Billeder:");
        foreach (var image in data.ImageUrls.Take(20))
        {
            Console.WriteLine($"- {image}");
        }

        break;
    }

    case "person":
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Mangler person-id.");
            return;
        }

        var id = args[1].Trim();
        var html = await client.GetPersonPageAsync(id, CancellationToken.None);
        var data = parser.ParsePersonPage(html, $"https://www.danskefilm.dk/skuespiller.php?id={id}");

        Console.WriteLine($"ID:             {data.Id}");
        Console.WriteLine($"Navn:           {data.Name}");
        Console.WriteLine($"Født:           {data.BirthDate}");
        Console.WriteLine($"Fødested:       {data.BirthPlace}");
        Console.WriteLine($"Død:            {data.DeathDate}");
        Console.WriteLine($"Gravsted:       {data.GraveSite}");
        Console.WriteLine($"Kilde:          {data.SourceUrl}");
        Console.WriteLine();

        Console.WriteLine("Biografi:");
        Console.WriteLine(data.Biography);
        Console.WriteLine();

        Console.WriteLine("Billeder:");
        foreach (var image in data.ImageUrls.Take(20))
        {
            Console.WriteLine($"- {image}");
        }

        Console.WriteLine();
        Console.WriteLine("Filmografi:");
        foreach (var item in data.Filmography.Take(60))
        {
            Console.WriteLine($"- {item.Title} ({item.Year}) => {item.Role} [film-id: {item.FilmId}]");
            Console.WriteLine($"    {item.Url}");
        }

        break;
    }

    default:
        Console.WriteLine($"Ukendt kommando: {command}");
        Console.WriteLine("Brug 'search', 'movie' eller 'person'.");
        break;
}
