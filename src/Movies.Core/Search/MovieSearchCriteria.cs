namespace Movies.Core.Search;

/// <summary>
/// Validated search criteria. Paging is 1-based.
/// </summary>
public sealed record MovieSearchCriteria(string? Search, int Page, int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const int MaxGenreLength = 50;

    /// <summary>Optional genre name to filter by (exact match, case-insensitive).</summary>
    public string? Genre { get; init; }

    public MovieSortField SortBy { get; init; } = MovieSortField.Title;

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
}
