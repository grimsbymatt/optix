namespace Movies.Api.Tests;

/// <summary>Hosts the API seeded from a CSV with no rows, to exercise the "not ready" path.</summary>
public sealed class EmptyDatabaseApiFactory : MoviesApiFactory
{
    protected override string SeedFileName => "empty.csv";
}
