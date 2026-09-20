using System.ComponentModel;
using Movies.Core.Search;

namespace Movies.Api.Endpoints;

/// <summary>Query-string parameters for <c>GET /api/movies</c>.</summary>
public sealed record SearchMoviesRequest(
    [property: Description("Text to find anywhere in the title (case- and accent-insensitive).")]
    string? Search,
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

        criteria = new MovieSearchCriteria(Search, page, pageSize);
        return errors.Count == 0;
    }
}
