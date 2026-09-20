# Movies API

A .NET 10 Web API for searching and paging through the
[9000+ Movies dataset](https://www.kaggle.com/datasets/disham993/9000-movies-dataset).
Built for the Optix technical test.

## Features

| Requirement | Status | How |
|---|---|---|
| Search movies by title | ✅ | `GET /api/movies?search=spider` (case- and accent-insensitive "contains") |
| Limit the number of results | ✅ | `pageSize` (1–100, default 20) |
| Page through the list | ✅ | `page` (1-based) with `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage` |
| Filter by genre | ✅ | `genre=Science Fiction` (exact name, case-insensitive). `GET /api/genres` lists the valid names |
| Filter by actor | ❓ | The dataset has no cast data (see [Notes](#notes-on-the-dataset)) |
| Sort by title / release date | ✅ | `sortBy=title\|releaseDate`, `sortDirection=asc\|desc` |

All the filters combine, and they're applied before paging, so `totalCount` and `totalPages` describe the filtered results.

Also included: `GET /api/movies/{id}`, `GET /api/genres`, `GET /health`, OpenAPI document and Scalar API explorer.

## Running it

### Docker (recommended)

```bash
docker compose up --build
```

Then open <http://localhost:8080>, which redirects to the Scalar API explorer.

### .NET SDK

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Movies.Api
```

The API explorer opens at <http://localhost:5080/scalar>. `src/Movies.Api/Movies.Api.http` has example requests for Visual Studio or VS Code.

### Tests

```bash
dotnet test
```

No Docker or external database is needed.

## API

### `GET /api/movies`

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `search` | string | – | Matches anywhere in the title. `pokemon` finds "Pokémon". Max 200 chars. |
| `genre` | string | – | Only movies in this genre. Exact name, case-insensitive, e.g. `action` or `Science Fiction`. An unknown genre returns an empty page. |
| `sortBy` | `title` \| `releaseDate` | `title` | Case-insensitive. |
| `sortDirection` | `asc` \| `desc` | `asc` | Case-insensitive. `ascending` and `descending` are also accepted. |
| `page` | int | 1 | 1-based. A page past the end returns an empty `items` list. |
| `pageSize` | int | 20 | 1–100. |

Ties are broken in a fixed order so paging is stable when values repeat:

- Sorting by title: then release date, then id (remakes share titles).
- Sorting by release date: then title, then id (many films share a date).

Example: `GET /api/movies?search=spider&genre=action&sortBy=releaseDate&sortDirection=desc&pageSize=5`

```json
{
  "items": [
    {
      "id": 1,
      "title": "Spider-Man: No Way Home",
      "releaseDate": "2021-12-15",
      "voteAverage": 8.3,
      "genres": ["Action", "Adventure", "Science Fiction"],
      "posterUrl": "https://image.tmdb.org/t/p/original/1g0dhYtq4irTY1GPXvft6k4YLjm.jpg"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

Invalid input returns `400` with an RFC 7807 validation problem. Unexpected errors return `500` with a problem
response that doesn't expose internal details.

### `GET /api/movies/{id}`

Returns the full movie, including overview, popularity, vote count and language. Returns `404` if the id doesn't exist.

### `GET /api/genres`

Every genre, ordered by name, with the number of movies in it. This lets a client (for example a UI dropdown) find the valid
`genre` values.

```json
[
  { "name": "Action", "movieCount": 2686 },
  { "name": "Adventure", "movieCount": 1853 }
]
```

## Architecture

```
src/
  Movies.Core             Entities, contracts (DTOs, IMovieCatalog), text normalisation. No dependencies.
  Movies.Infrastructure   EF Core + SQLite, CSV import, query implementation.
  Movies.Api              Minimal API endpoints, validation, error handling, OpenAPI, composition root.
tests/
  Movies.Infrastructure.Tests   CSV parsing (including the full real dataset) and SQL queries against SQLite.
  Movies.Api.Tests              End-to-end HTTP tests via WebApplicationFactory.
docs/adr/                 Architecture decision records.
resources/                The source dataset.
```

The structure is deliberately small. There are three projects with one-way dependencies (Api → Infrastructure → Core), and no
repositories or mediator layers wrapped around EF Core. `IMovieCatalog` is the single seam between the HTTP layer and data access.

### Key decisions

- **SQLite in-memory, seeded at start-up.** It's a real SQL engine with no extra infrastructure. A singleton keep-alive connection
  stops the in-memory database from disappearing. See [ADR 0001](docs/adr/0001-sqlite-in-memory-database.md) for the reasoning and
  the migration path to PostgreSQL.
- **Case- and accent-insensitive search.** A `SearchTitle` column holds a lower-cased, accent-stripped copy of each title, and
  search terms are normalised the same way. User input is escaped, so `100%` matches literally.
- **Case-insensitive genre filter in the database.** `Genre.Name` uses SQLite's `NOCASE` collation, so `genre=action` matches
  "Action" without normalising values in code. Genre names are all ASCII, so `NOCASE`'s ASCII-only case folding is enough.
- **Sort options are parsed and validated, not bound as enums.** An invalid value such as `sortBy=rating` or `sortBy=1` returns a
  clear 400 validation problem instead of being ignored or quietly mapped to an enum number.
- **Offset paging.** This fits "page through the list" and a dataset of this size. Keyset paging would be the next step for very
  large tables.
- **Tolerant import.** Invalid CSV rows are logged and skipped rather than stopping start-up.
- **Central package management** (`Directory.Packages.props`) and shared build settings (`Directory.Build.props`).

## Notes on the dataset

- 9,827 movies with release date, title, overview, popularity, vote count, vote average, original language, genres (a
  comma-separated list, normalised into `Genre` tables) and poster URL.
- **Stray carriage returns.** One row ("Pixie Hollow Bake Off") has bare `\r` characters inside an unquoted field. Many CSV parsers
  treat these as line breaks, which splits the row. The importer sets the record separator to `\n` explicitly, and
  `.gitattributes` stops Git from changing the file's line endings. A test checks that every row of the real file imports.
- **No cast or actor data.** The "filter by actors" requirement would need the data enriched (for example from the TMDB API,
  which the poster URLs come from) or a different dataset.
