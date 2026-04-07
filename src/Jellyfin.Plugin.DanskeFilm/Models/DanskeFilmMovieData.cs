namespace Jellyfin.Plugin.DanskeFilm.Models;

public class DanskeFilmMovieData
{
    public string? Id { get; set; }

    public string? SourceUrl { get; set; }

    public string? Title { get; set; }

    public int? Year { get; set; }

    public string? Overview { get; set; }

    public string? Director { get; set; }

    public List<DanskeFilmCastMember> Cast { get; set; } = [];

    public List<string> Writers { get; set; } = [];

    public List<string> Studios { get; set; } = [];

    public List<string> Genres { get; set; } = [];

    public string? PosterUrl { get; set; }

    public List<string> ImageUrls { get; set; } = [];

    public string? PremiereDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public string? TrailerUrl { get; set; }
}
