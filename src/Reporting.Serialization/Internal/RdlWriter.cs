using System.Globalization;
using System.Xml.Linq;
using Reporting.Geometry;

namespace Reporting.Serialization.Internal;

/// <summary>
/// Builds the RDL (<c>&lt;Report&gt;</c>) XML tree for a <see cref="ReportDefinition"/> — the structural
/// inverse of <see cref="RdlImporter"/>. Emits the official SSRS namespace
/// (<c>…/sqlserver/reporting/2016/01/reportdefinition</c>), the SAME one the importer reads, so a report can
/// round-trip <c>.rdl → import → edit → export → .rdl</c> and interoperate with SSRS / Report Builder.
/// <para>PR1 emits the report skeleton (page setup + empty body + report metadata). ReportItems, DataSets,
/// parameters and Tablix are layered on in later phases.</para>
/// </summary>
internal static class RdlWriter
{
    internal static readonly XNamespace Rdl =
        "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

    public static XDocument Write(ReportDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var page = def.PageSetup;
        var report = new XElement(Rdl + "Report");

        // Body — the design canvas. ReportItems are populated in the next phase; for now it carries only its
        // height (RDL requires Body.Height). Continuous (thermal) paper has no page height → use a default.
        var bodyHeight = page.ContentHeight.ToMm() > 0 ? page.ContentHeight : Unit.FromMm(100);
        report.Add(new XElement(Rdl + "Body",
            new XElement(Rdl + "Height", Size(bodyHeight)),
            new XElement(Rdl + "ReportItems")));

        report.Add(new XElement(Rdl + "Width", Size(page.ContentWidth)));

        var pageEl = new XElement(Rdl + "Page",
            new XElement(Rdl + "PageHeight", Size(page.PageHeight)),
            new XElement(Rdl + "PageWidth", Size(page.PageWidth)),
            new XElement(Rdl + "LeftMargin", Size(page.Margins.Left)),
            new XElement(Rdl + "RightMargin", Size(page.Margins.Right)),
            new XElement(Rdl + "TopMargin", Size(page.Margins.Top)),
            new XElement(Rdl + "BottomMargin", Size(page.Margins.Bottom)));
        if (page.Columns > 1)
        {
            pageEl.Add(new XElement(Rdl + "Columns", page.Columns.ToString(CultureInfo.InvariantCulture)));
            pageEl.Add(new XElement(Rdl + "ColumnSpacing", Size(page.ColumnSpacing)));
        }
        report.Add(pageEl);

        // Report-level metadata the importer round-trips through Metadata.
        AddIfPresent(report, def, "Language", "Language");
        AddIfPresent(report, def, "Description", "Description");
        AddIfPresent(report, def, "Author", "Author");

        return new XDocument(new XDeclaration("1.0", "utf-8", null), report);
    }

    private static void AddIfPresent(XElement report, ReportDefinition def, string metaKey, string rdlElement)
    {
        if (def.Metadata.TryGetValue(metaKey, out var value) && !string.IsNullOrEmpty(value))
        {
            report.Add(new XElement(Rdl + rdlElement, value));
        }
    }

    /// <summary>An RDL size string (e.g. <c>"210mm"</c>) — RDL's <c>RdlSize</c> accepts mm/cm/in/pt/pc.
    /// Always invariant-formatted so the XML is culture-stable.</summary>
    internal static string Size(Unit u) =>
        u.ToMm().ToString("0.####", CultureInfo.InvariantCulture) + "mm";
}
