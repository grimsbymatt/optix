namespace Movies.Core.Entities;

public sealed class Movie
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// Lower-cased, accent-stripped copy of <see cref="Title"/> used for
    /// case- and accent-insensitive searching and for stable ordering.
    /// SQLite's LIKE only folds ASCII case, so this is computed on import.
    /// </summary>
    public required string SearchTitle { get; set; }

    public string Overview { get; set; } = string.Empty;

    public DateOnly ReleaseDate { get; set; }

    public double Popularity { get; set; }

    public int VoteCount { get; set; }

    public double VoteAverage { get; set; }

    public string OriginalLanguage { get; set; } = string.Empty;

    public string? PosterUrl { get; set; }

    public ICollection<Genre> Genres { get; set; } = [];
}
