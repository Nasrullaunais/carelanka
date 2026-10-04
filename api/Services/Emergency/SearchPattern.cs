namespace CareLanka.Api.Services.Emergency;

// A typed % or _ is a character to find, not a wildcard, so it is escaped before ILIKE sees it.
public static class SearchPattern
{
    public const string Escape = "\\";

    public static string Contains(string term)
        => $"%{term.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_")}%";
}
