using System.Data;
using Microsoft.Data.Sqlite;

namespace Movies.Infrastructure.Data;

/// <summary>
/// Holds a single connection open for the lifetime of the application so that the
/// shared-cache in-memory database is not destroyed when EF Core closes its connections.
/// Registered as a singleton; it is never used to run queries (connections are not thread-safe),
/// each DbContext opens its own connection to the same named database.
/// </summary>
public sealed class SqliteKeepAlive(string connectionString) : IDisposable
{
    private readonly SqliteConnection _connection = new(connectionString);
    private readonly Lock _lock = new();

    public void EnsureOpen()
    {
        lock (_lock)
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }
    }

    public void Dispose() => _connection.Dispose();
}
