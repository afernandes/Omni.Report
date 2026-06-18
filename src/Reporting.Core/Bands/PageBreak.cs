namespace Reporting.Bands;

/// <summary>RDL-compatible page-break rule for a data region (band, group). Mirrors the
/// <c>&lt;PageBreak&gt;</c> element with <c>BreakLocation</c> attribute in the Microsoft
/// RDL spec, plus our own <see cref="None"/> default for backwards compatibility.</summary>
/// <remarks>
/// <para>Applied by the paginator before/after emitting the band's content:
/// <list type="bullet">
/// <item><see cref="None"/> — paginator behaves normally; band flows with surrounding content.</item>
/// <item><see cref="Start"/> — paginator inserts a hard page break BEFORE the band's
/// first row/render. Equivalent to RDL <c>BreakLocation="Start"</c>.</item>
/// <item><see cref="End"/> — page break AFTER the band's last row. RDL
/// <c>BreakLocation="End"</c>.</item>
/// <item><see cref="StartAndEnd"/> — break before AND after. RDL
/// <c>BreakLocation="StartAndEnd"</c>.</item>
/// <item><see cref="Between"/> — applies to groups only: inserts a break between each group
/// instance. RDL <c>BreakLocation="Between"</c>. On non-group bands behaves like
/// <see cref="None"/>.</item>
/// </list>
/// </para>
///
/// <para>For groups, the legacy <c>NewPageBefore</c>/<c>NewPageAfter</c> bool pair on
/// <see cref="GroupBand"/> is still honored — the paginator unifies them with this enum at
/// runtime: any non-<see cref="None"/> value here OVERRIDES the legacy bools. This lets
/// existing reports keep working while new ones use the RDL-idiomatic single field.</para>
/// </remarks>
public enum PageBreak
{
    /// <summary>No forced page break.</summary>
    None = 0,

    /// <summary>Break before the band's first instance.</summary>
    Start,

    /// <summary>Break after the band's last instance.</summary>
    End,

    /// <summary>Break before AND after — band always renders on its own page.</summary>
    StartAndEnd,

    /// <summary>Between consecutive group instances (group bands only).</summary>
    Between,
}
