using Microsoft.EntityFrameworkCore;
using Movies.Core.Entities;
using Movies.Core.Search;
using Movies.Core.Text;
using Movies.Infrastructure.Data;

namespace Movies.Infrastructure.Search;

internal sealed class MovieCatalog(MoviesDbContext dbContext) : IMovieCatalog
{
    public async Task<PagedResult<MovieSummary>> SearchAsync(MovieSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = ApplyFilters(dbContext.Movies.AsNoTracking(), criteria);

        var totalCount = await query.CountAsync(cancellationToken);

        // Avoid a pointless query (and int overflow in Skip) when the page is past the end.
        var offset = (long)(criteria.Page - 1) * criteria.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<MovieSummary>([], criteria.Page, criteria.PageSize, totalCount);
        }

        var items = await ApplySort(query, criteria)
            .Skip((int)offset)
            .Take(criteria.PageSize)
            .Select(m => new MovieSummary(
                m.Id,
                m.Title,
                m.ReleaseDate,
                m.VoteAverage,
                m.Genres.OrderBy(g => g.Name).Select(g => g.Name).ToList(),
                m.PosterUrl))
            .ToListAsync(cancellationToken);

        return new PagedResult<MovieSummary>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public Task<MovieDetails?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Movies
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MovieDetails(
                m.Id,
                m.Title,
                m.Overview,
                m.ReleaseDate,
                m.Popularity,
                m.VoteCount,
                m.VoteAverage,
                m.OriginalLanguage,
                m.Genres.OrderBy(g => g.Name).Select(g => g.Name).ToList(),
                m.PosterUrl))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GenreSummary>> GetGenresAsync(CancellationToken cancellationToken) =>
        await dbContext.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GenreSummary(g.Name, g.Movies.Count))
            .ToListAsync(cancellationToken);

    private static IQueryable<Movie> ApplyFilters(IQueryable<Movie> query, MovieSearchCriteria criteria)
    {
        var term = TextNormalizer.Normalize(criteria.Search);
        if (term.Length > 0)
        {
            var pattern = SqlLike.Contains(term);
            query = query.Where(m => EF.Functions.Like(m.SearchTitle, pattern, SqlLike.EscapeCharacter));
        }

        var genre = criteria.Genre?.Trim();
        if (!string.IsNullOrEmpty(genre))
        {
            // Genre.Name uses the NOCASE collation, so this comparison is case-insensitive.
            query = query.Where(m => m.Genres.Any(g => g.Name == genre));
        }

        return query;
    }

    /// <summary>
    /// Orders by the requested field, then by fixed ascending tie-breakers so that paging
    /// is deterministic when values repeat (remakes share titles, many films share a release date).
    /// </summary>
    private static IOrderedQueryable<Movie> ApplySort(IQueryable<Movie> query, MovieSearchCriteria criteria)
    {
        var descending = criteria.SortDirection == SortDirection.Descending;

        return criteria.SortBy switch
        {
            MovieSortField.ReleaseDate => (descending
                    ? query.OrderByDescending(m => m.ReleaseDate)
                    : query.OrderBy(m => m.ReleaseDate))
                .ThenBy(m => m.SearchTitle)
                .ThenBy(m => m.Id),

            MovieSortField.Title => (descending
                    ? query.OrderByDescending(m => m.SearchTitle)
                    : query.OrderBy(m => m.SearchTitle))
                .ThenBy(m => m.ReleaseDate)
                .ThenBy(m => m.Id),

            _ => throw new ArgumentOutOfRangeException(nameof(criteria), criteria.SortBy, "Unsupported sort field."),
        };
    }
}
