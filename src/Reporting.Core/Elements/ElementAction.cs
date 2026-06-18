using Reporting.Common;

namespace Reporting.Elements;

/// <summary>RDL-compatible action attached to a <see cref="ReportElement"/>. Mirrors the
/// <c>&lt;Action&gt;</c> element in the Microsoft RDL specification: an element can carry at
/// most one action that fires when the user clicks/taps it in an interactive renderer
/// (Viewer, HTML export, PDF export with hyperlink annotations).
/// </summary>
/// <remarks>
/// <para>The action is a sum type with exactly one of three flavours populated. We model it
/// as a record with optional fields rather than separate subclasses because RDL itself uses
/// the same shape (mutually exclusive child elements <c>&lt;Hyperlink&gt;</c>,
/// <c>&lt;BookmarkLink&gt;</c>, <c>&lt;Drillthrough&gt;</c>) — preserving the source structure
/// keeps round-trip lossless.</para>
///
/// <para>Renderers consume the action via <see cref="Kind"/>:
/// <list type="bullet">
/// <item><see cref="ActionKind.Hyperlink"/> — open <see cref="Hyperlink"/> in a new tab (or
/// emit a PDF link annotation). Both literal URLs and expressions that evaluate to URLs
/// are supported.</item>
/// <item><see cref="ActionKind.BookmarkLink"/> — jump to the element whose
/// <see cref="ReportElement.Bookmark"/> equals <see cref="BookmarkId"/>. Works inside the
/// rendered document only (PDF outlines, Viewer scroll).</item>
/// <item><see cref="ActionKind.DrillthroughReport"/> — open another report identified by
/// <see cref="DrillthroughReportName"/>, passing <see cref="DrillthroughParameters"/> as
/// the report parameters. Host-mediated: the Viewer raises an event the host handles.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record ElementAction(
    ActionKind Kind,
    string? Hyperlink = null,
    string? BookmarkId = null,
    string? DrillthroughReportName = null,
    EquatableArray<DrillthroughParameter> DrillthroughParameters = default)
{
    /// <summary>Convenience constructor for a hyperlink action.</summary>
    public static ElementAction ToUrl(string urlOrExpression)
        => new(ActionKind.Hyperlink, Hyperlink: urlOrExpression);

    /// <summary>Convenience constructor for a bookmark jump action.</summary>
    public static ElementAction ToBookmark(string bookmarkId)
        => new(ActionKind.BookmarkLink, BookmarkId: bookmarkId);

    /// <summary>Convenience constructor for a drill-through action.</summary>
    public static ElementAction ToDrillthrough(string reportName, params DrillthroughParameter[] parameters)
        => new(ActionKind.DrillthroughReport,
               DrillthroughReportName: reportName,
               DrillthroughParameters: new EquatableArray<DrillthroughParameter>(parameters));
}

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

/// <summary>Parameter passed to a drillthrough target report. Mirrors RDL
/// <c>&lt;Parameter&gt;</c> inside <c>&lt;Drillthrough&gt;</c>.</summary>
/// <param name="Name">Name of the parameter on the target report.</param>
/// <param name="Value">Expression evaluated in the SOURCE report's context; the value is
/// passed to the target report as the parameter's bound value.</param>
/// <param name="Omit">If true, the parameter is not passed — useful for conditionally
/// suppressing parameters (RDL <c>&lt;Omit&gt;</c>).</param>
public sealed record DrillthroughParameter(string Name, string Value, bool Omit = false);
