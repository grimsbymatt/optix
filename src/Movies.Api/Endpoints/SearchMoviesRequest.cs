using System.ComponentModel;
using Movies.Core.Search;
using SortOrder = Movies.Core.Search.SortDirection;

namespace Movies.Api.Endpoints;

/// <summary>Query-string parameters for <c>GET /api/movies</c>.</summary>
/// <remarks>
/// Sort options are bound as strings and parsed here, rather than as enums, so that invalid values
/// produce a clear validation message and numeric values such as "1" are not silently accepted.
/// </remarks>
public sealed record SearchMoviesRequest(
    [property: Description("Text to find anywhere in the title (case- and accent-insensitive).")]
    string? Search,
    [property: Description("Only return movies in this genre (case-insensitive), e.g. 'Science Fiction'. See GET /api/genres.")]
    string? Genre,
    [property: Description("Sort field: 'title' (default) or 'releaseDate'.")]
    string? SortBy,
    [property: Description("Sort direction: 'asc' (default) or 'desc'.")]
    string? SortDirection,
    [property: Description("1-based page number. Defaults to 1.")]
    int? Page,
    [property: Description("Results per page, 1-100. Defaults to 20.")]
    int? PageSize)
{
    public bool TryGetCriteria(out MovieSearchCriteria criteria, out Dictionary<string, string[]> errors)
    {
        errors = [];

        var page = Page ?? MovieSearchCriteria.DefaultPage;
        var pageSize = PageSize ?? MovieSearchCriteria.DefaultPageSize;

        if (page < 1)
        {
            errors[nameof(Page)] = ["Page must be 1 or greater."];
        }

        if (pageSize is < 1 or > MovieSearchCriteria.MaxPageSize)
        {
            errors[nameof(PageSize)] = [$"PageSize must be between 1 and {MovieSearchCriteria.MaxPageSize}."];
        }

        if (Search?.Length > MovieSearchCriteria.MaxSearchLength)
        {
            errors[nameof(Search)] = [$"Search term must be {MovieSearchCriteria.MaxSearchLength} characters or fewer."];
        }

        if (Genre?.Length > MovieSearchCriteria.MaxGenreLength)
        {
            errors[nameof(Genre)] = [$"Genre must be {MovieSearchCriteria.MaxGenreLength} characters or fewer."];
        }

        if (!TryParseSortField(SortBy, out var sortBy))
        {
            errors[nameof(SortBy)] = ["SortBy must be 'title' or 'releaseDate'."];
        }

        if (!TryParseSortDirection(SortDirection, out var sortDirection))
        {
            errors[nameof(SortDirection)] = ["SortDirection must be 'asc' or 'desc'."];
        }

        criteria = new MovieSearchCriteria(Search, page, pageSize)
        {
            Genre = Genre,
            SortBy = sortBy,
            SortDirection = sortDirection,
        };

        return errors.Count == 0;
    }

    private static bool TryParseSortField(string? value, out MovieSortField field)
    {
        field = MovieSortField.Title;

        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "title":
                return true;
            case "releasedate":
                field = MovieSortField.ReleaseDate;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseSortDirection(string? value, out SortOrder direction)
    {
        direction = SortOrder.Ascending;

        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "asc" or "ascending":
                return true;
            case "desc" or "descending":
                direction = SortOrder.Descending;
                return true;
            default:
                return false;
        }
    }
}
