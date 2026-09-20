using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Movies.Infrastructure.Seeding;

/// <summary>
/// Parses the Kaggle "9000+ Movies" CSV. Invalid rows are skipped and reported rather than
/// failing the whole import.
/// </summary>
internal static class MovieCsvReader
{
    private static readonly string[] RequiredColumns =
    [
        "Release_Date", "Title", "Overview", "Popularity", "Vote_Count",
        "Vote_Average", "Original_Language", "Genre", "Poster_Url",
    ];

    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        // The dataset contains stray '\r' characters inside unquoted Overview fields.
        // Setting NewLine explicitly stops the parser treating a bare '\r' as the end of a record.
        NewLine = "\n",
        // ...which means a CRLF file leaves '\r' on the last header; trim so it still matches.
        PrepareHeaderForMatch = args => args.Header.Trim(),
        // Rows are validated individually below, so don't throw on quirks.
        BadDataFound = null,
        MissingFieldFound = null,
    };

    public static async Task<MovieCsvReadResult> ReadAsync(TextReader reader, CancellationToken cancellationToken = default)
    {
        var rows = new List<MovieCsvRow>();
        var errors = new List<string>();

        using var csv = new CsvReader(reader, Configuration);

        if (!await csv.ReadAsync())
        {
            return new MovieCsvReadResult(rows, errors);
        }

        csv.ReadHeader();
        var header = new HashSet<string>(csv.HeaderRecord?.Select(Clean) ?? [], StringComparer.OrdinalIgnoreCase);
        var missing = RequiredColumns.Where(column => !header.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"Movie CSV is missing required column(s): {string.Join(", ", missing)}.");
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryParseRow(csv, out var row, out var error))
            {
                rows.Add(row);
            }
            else
            {
                errors.Add($"Row {csv.Parser.Row}: {error}");
            }
        }

        return new MovieCsvReadResult(rows, errors);
    }

    private static bool TryParseRow(CsvReader csv, out MovieCsvRow row, out string error)
    {
        row = null!;
        error = string.Empty;

        var title = Clean(csv.GetField("Title"));
        if (title.Length == 0)
        {
            error = "Title is empty.";
            return false;
        }

        if (!DateOnly.TryParseExact(Clean(csv.GetField("Release_Date")), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var releaseDate))
        {
            error = $"'{title}' has an invalid Release_Date.";
            return false;
        }

        if (!TryParseDouble(csv.GetField("Popularity"), out var popularity) ||
            !TryParseDouble(csv.GetField("Vote_Average"), out var voteAverage) ||
            !int.TryParse(Clean(csv.GetField("Vote_Count")), NumberStyles.Integer, CultureInfo.InvariantCulture, out var voteCount))
        {
            error = $"'{title}' has an invalid numeric value.";
            return false;
        }

        var genres = Clean(csv.GetField("Genre"))
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var posterUrl = Clean(csv.GetField("Poster_Url"));

        row = new MovieCsvRow(
            releaseDate,
            title,
            Clean(csv.GetField("Overview")),
            popularity,
            voteCount,
            voteAverage,
            Clean(csv.GetField("Original_Language")),
            genres,
            Uri.TryCreate(posterUrl, UriKind.Absolute, out _) ? posterUrl : null);

        return true;
    }

    private static bool TryParseDouble(string? value, out double result) =>
        double.TryParse(Clean(value), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    // Normalises stray '\r' (and a trailing '\r' if the file has CRLF line endings) and trims whitespace.
    private static string Clean(string? value) => value?.ReplaceLineEndings("\n").Trim() ?? string.Empty;
}
