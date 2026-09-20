namespace Movies.Infrastructure.Search;

/// <summary>Helpers for building safe LIKE patterns from user input.</summary>
internal static class SqlLike
{
    public const string EscapeCharacter = "\\";

    /// <summary>Escapes LIKE wildcards so "100%" matches a literal percent sign.</summary>
    public static string Escape(string value) =>
        value
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);

    public static string Contains(string value) => $"%{Escape(value)}%";
}
