using Reporting.Common;

namespace Reporting.Elements;

/// <summary>Selects which action variant is active.</summary>
public enum ActionKind
{
    /// <summary>URL or expression-producing-URL action.</summary>
    Hyperlink,

    /// <summary>Jump to another element by its <see cref="ReportElement.Bookmark"/> value.</summary>
    BookmarkLink,

    /// <summary>Open another report by name with parameter values.</summary>
    DrillthroughReport,
}
