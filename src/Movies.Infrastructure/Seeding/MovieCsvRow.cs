namespace Movies.Infrastructure.Seeding;

internal sealed record MovieCsvRow(
    DateOnly ReleaseDate,
    string Title,
    string Overview,
    double Popularity,
    int VoteCount,
    double VoteAverage,
    string OriginalLanguage,
    IReadOnlyList<string> Genres,
    string? PosterUrl);

internal sealed record MovieCsvReadResult(IReadOnlyList<MovieCsvRow> Rows, IReadOnlyList<string> Errors);
