namespace Movies.Core.Search;

public sealed record MovieDetails(
    int Id,
    string Title,
    string Overview,
    DateOnly ReleaseDate,
    double Popularity,
    int VoteCount,
    double VoteAverage,
    string OriginalLanguage,
    IReadOnlyList<string> Genres,
    string? PosterUrl);
