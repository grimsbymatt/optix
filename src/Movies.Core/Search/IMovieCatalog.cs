namespace Movies.Core.Search;

/// <summary>Read-side queries over the movie catalogue.</summary>
public interface IMovieCatalog
{
    Task<PagedResult<MovieSummary>> SearchAsync(MovieSearchCriteria criteria, CancellationToken cancellationToken);

    Task<MovieDetails?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GenreSummary>> GetGenresAsync(CancellationToken cancellationToken);
}
