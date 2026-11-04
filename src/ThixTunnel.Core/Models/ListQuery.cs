namespace ThixTunnel.Core.Models;

public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

/// <summary>Search text, filter and sort of a list page (the toolbar above every list).</summary>
public sealed record ListQuery(string SearchText = "", string Filter = "", string SortBy = "", SortDirection Direction = SortDirection.Ascending);
