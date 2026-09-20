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
        IQueryable<Movie> query = dbContext.Movies.AsNoTracking();

        var term = TextNormalizer.Normalize(criteria.Search);
        if (term.Length > 0)
        {
            var pattern = SqlLike.Contains(term);
            query = query.Where(m => EF.Functions.Like(m.SearchTitle, pattern, SqlLike.EscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Avoid a pointless query (and int overflow in Skip) when the page is past the end.
        var offset = (long)(criteria.Page - 1) * criteria.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<MovieSummary>([], criteria.Page, criteria.PageSize, totalCount);
        }

        // Id is a tie-breaker so paging is deterministic when titles repeat (e.g. remakes).
        var items = await query
            .OrderBy(m => m.SearchTitle)
            .ThenBy(m => m.ReleaseDate)
            .ThenBy(m => m.Id)
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
}
