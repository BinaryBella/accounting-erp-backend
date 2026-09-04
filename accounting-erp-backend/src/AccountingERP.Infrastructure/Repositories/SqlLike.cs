namespace AccountingERP.Infrastructure.Repositories;

internal static class SqlLike
{
    /// <summary>
    /// Turns a free-text search term into a <c>%term%</c> pattern with the LIKE
    /// wildcards (<c>% _ [</c>) and the escape char itself neutralised. Pair with
    /// <c>ESCAPE '\'</c> in the query. Returns null for a blank term so the
    /// <c>@SearchLike IS NULL</c> branch skips the filter.
    /// </summary>
    public static string? Contains(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return null;

        var escaped = term.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

        return $"%{escaped}%";
    }
}
