namespace Jellyfin.Plugin.DanskeFilm.Models;

public class DanskeFilmPersonData
{
    public string? Id { get; set; }

    public string? SourceUrl { get; set; }

    public string? Name { get; set; }

    public string? Biography { get; set; }

    public string? BirthDate { get; set; }

    public string? BirthPlace { get; set; }

    public string? DeathDate { get; set; }

    public string? GraveSite { get; set; }

    public List<string> ImageUrls { get; set; } = [];

    public List<DanskeFilmFilmographyEntry> Filmography { get; set; } = [];
}
