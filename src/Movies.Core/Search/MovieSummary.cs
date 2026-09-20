namespace Movies.Core.Search;

/// <summary>Lightweight representation of a movie for search results.</summary>
public sealed record MovieSummary(
    int Id,
    string Title,
    DateOnly ReleaseDate,
    double VoteAverage,
    IReadOnlyList<string> Genres,
    string? PosterUrl);
