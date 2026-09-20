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
}
