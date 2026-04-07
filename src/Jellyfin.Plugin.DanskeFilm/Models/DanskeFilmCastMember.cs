namespace Jellyfin.Plugin.DanskeFilm.Models;

public class DanskeFilmCastMember
{
    public string Name { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string? ProfileUrl { get; set; }

    public string? PersonId { get; set; }
}
