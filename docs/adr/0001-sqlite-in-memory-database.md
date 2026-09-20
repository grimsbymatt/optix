# ADR 0001: SQLite in-memory database, seeded at start-up

**Status:** Accepted

## Context

The API serves a fixed, read-only dataset of about 9,800 movies from a CSV file. The brief asks for a
database of our choice and says to keep things simple (KISS) and easy to deploy.

## Options considered

| Option | Notes |
|---|---|
| EF Core InMemory provider | Not relational and does not translate LINQ to SQL, so it hides query bugs. Rejected. |
| PostgreSQL / SQL Server container | Production-grade, but adds a second container, connection management and migrations for data that never changes. |
| **SQLite, in-memory, seeded at start-up** | Real SQL engine, no extra infrastructure, one container. |

## Decision

Use SQLite through EF Core with a named, shared-cache in-memory database
(`Data Source=movies;Mode=Memory;Cache=Shared`).

- An in-memory SQLite database only exists while a connection is open. A singleton `SqliteKeepAlive`
  holds one connection open for the app's lifetime. Each `DbContext` opens its own connection to the
  same database, because connections aren't thread-safe.
- The schema is created with `EnsureCreated()` and loaded from the CSV before the app starts accepting
  requests. Migrations add nothing here because the database is rebuilt on every start.
- Searches use a normalised `SearchTitle` column (lower-case, accents removed). SQLite's `LIKE` only
  ignores case for ASCII, and the dataset has many accented titles.
- Numbers used for sorting are `double`, not `decimal`, because EF Core can't sort by `decimal` columns in SQLite.

## Consequences

- Start-up includes a short seed (about 1–2 seconds). Each instance holds its own copy of the data. That's acceptable for read-only data.
- Tests use the same engine: the query tests use a private `:memory:` database, and the API tests use a uniquely named shared database per test class.
- **When to revisit:** once data becomes writable or shared between instances, move to PostgreSQL.
  Data access is behind `IMovieCatalog` and EF Core, so the change is mostly contained in
  `Movies.Infrastructure`. The main work would be switching search to `ILIKE` with a `pg_trgm` index
  and adopting migrations.
