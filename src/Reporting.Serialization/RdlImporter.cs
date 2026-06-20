using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Serialization.Internal;

namespace Reporting.Serialization;

/// <summary>
/// Imports an SSRS <c>.rdl</c> (Report Definition Language, XML) into an OmniReport
/// <see cref="ReportDefinition"/> — the literal "RDL compatibility" path, enabling migration from
/// SSRS. The result is a normal definition: it renders through the engine, opens in the Designer, and
/// is equivalent to what code-first / low-level authoring would produce.
/// </summary>
/// <remarks>
/// <para>First-cut coverage: page setup (size + margins), report parameters (incl. Available Values —
/// static <c>&lt;ParameterValues&gt;</c> and query <c>&lt;DataSetReference&gt;</c>), and free-form
/// report items in the Body / Page header / Page footer — <c>Textbox</c> (→ TextBox/Label),
/// <c>Line</c>, <c>Rectangle</c> (shape + nested items, offset to absolute), and external
/// <c>Image</c>. VB expressions (<c>=Fields!X.Value</c>, …) are translated by
/// <see cref="RdlExpression"/>.</para>
/// <para>Not yet imported (follow-ups): Tablix/Matrix and Chart data regions, embedded-image bytes,
/// dataset queries, subreports, and rich styling. These are skipped, not errored — the structural
/// import always succeeds.</para>
/// </remarks>
public sealed class RdlImporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public ReportDefinition Import(Stream stream, string? reportName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Import(XDocument.Load(stream, LoadOptions.None), reportName);
    }

    public ReportDefinition ImportXml(string xml, string? reportName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        return Import(XDocument.Parse(xml), reportName);
    }

    public ReportDefinition Import(XDocument document, string? reportName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var report = document.Root ?? throw new FormatException("Empty RDL document.");
        if (report.Name.LocalName != "Report")
        {
            throw new FormatException($"Root element must be <Report>, found <{report.Name.LocalName}>.");
        }

        var page = El(report, "Page");
        var marginHost = page ?? report;
        var pageWidth = ParseSize(Val(marginHost, "PageWidth")) ?? PaperSize.A4.Width;
        var pageHeight = ParseSize(Val(marginHost, "PageHeight")) ?? PaperSize.A4.Height;
        var margins = new Thickness(
            ParseSize(Val(marginHost, "LeftMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "TopMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "RightMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "BottomMargin")) ?? Unit.Zero);
        var pageSetup = new PageSetup(new PaperSize("Imported", pageWidth, pageHeight), Orientation.Portrait, margins);

        var parameters = (El(report, "ReportParameters")?.Elements()
                .Where(e => e.Name.LocalName == "ReportParameter")
                .Select(ReadParameter).ToArray())
            ?? Array.Empty<ReportParameter>();

        // Body's free report items render once → a ReportHeader band. Page header/footer map directly.
        var body = El(report, "Body");
        var reportHeader = BandFrom(El(body, "ReportItems"), Val(body, "Height"), BandKind.ReportHeader);
        var pageHeaderEl = El(page, "PageHeader");
        var pageHeader = BandFrom(El(pageHeaderEl, "ReportItems"), Val(pageHeaderEl, "Height"), BandKind.PageHeader);
        var pageFooterEl = El(page, "PageFooter");
        var pageFooter = BandFrom(El(pageFooterEl, "ReportItems"), Val(pageFooterEl, "Height"), BandKind.PageFooter);

        return new ReportDefinition(reportName ?? "RdlReport", pageSetup, DetailBand.Empty)
        {
            Parameters = new EquatableArray<ReportParameter>(parameters),
            ReportHeader = reportHeader,
            PageHeader = pageHeader,
            PageFooter = pageFooter,
        };
    }

    private static ReportBand? BandFrom(XElement? reportItems, string? heightRaw, BandKind kind)
    {
        if (reportItems is null)
        {
            return null;
        }
        var elements = new List<ReportElement>();
        foreach (var item in reportItems.Elements())
        {
            AddItem(item, Unit.Zero, Unit.Zero, elements);
        }
        if (elements.Count == 0)
        {
            return null;
        }
        var height = ParseSize(heightRaw) ?? BoundsHeight(elements);
        return new ReportBand(kind, height, new EquatableArray<ReportElement>(elements));
    }

    // Maps one RDL report item to OmniReport element(s), offsetting by (dx, dy) so nested items inside a
    // Rectangle become absolute. Unknown item kinds are skipped (structural import never fails on them).
    private static void AddItem(XElement item, Unit dx, Unit dy, List<ReportElement> into)
    {
        var bounds = Bounds(item, dx, dy);
        switch (item.Name.LocalName)
        {
            case "Textbox":
                into.Add(TextItem(item, bounds));
                break;
            case "Line":
                into.Add(new LineElement { Bounds = bounds });
                break;
            case "Image":
                into.Add(ImageItem(item, bounds));
                break;
            case "Rectangle":
                into.Add(new RectangleElement { Bounds = bounds });
                // Recurse: nested items are positioned relative to the rectangle in RDL.
                foreach (var child in El(item, "ReportItems")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    AddItem(child, bounds.X, bounds.Y, into);
                }
                break;
            // Tablix, Chart, Subreport, … — follow-ups; skipped, not errored.
        }
    }

    private static ReportElement TextItem(XElement item, Rectangle bounds)
    {
        var raw = TextboxValue(item);
        if (RdlExpression.IsExpression(raw))
        {
            return new TextBoxElement { Expression = RdlExpression.Convert(raw), Bounds = bounds };
        }
        return new LabelElement { Text = raw ?? string.Empty, Bounds = bounds };
    }

    // RDL 2016 nests the value under Paragraphs/Paragraph/TextRuns/TextRun/Value; RDL 2008 uses a direct
    // <Value>. Returns the first value found.
    private static string? TextboxValue(XElement textbox)
    {
        var run = El(El(El(El(textbox, "Paragraphs"), "Paragraph"), "TextRuns"), "TextRun");
        return Val(run, "Value") ?? Val(textbox, "Value");
    }

    private static ReportElement ImageItem(XElement item, Rectangle bounds)
    {
        var source = Val(item, "Source");
        var value = Val(item, "Value");
        if (string.Equals(source, "External", StringComparison.OrdinalIgnoreCase))
        {
            return RdlExpression.IsExpression(value)
                ? new ImageElement { Source = ImageSourceKind.Expression, Expression = RdlExpression.Convert(value), Bounds = bounds }
                : new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
        }
        // Embedded/Database image bytes are a follow-up — keep the reference name as the path.
        return new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
    }

    private static ReportParameter ReadParameter(XElement el)
    {
        var name = el.Attribute("Name")?.Value ?? throw new FormatException("ReportParameter missing Name.");
        var type = MapType(Val(el, "DataType"));
        var prompt = Val(el, "Prompt");
        var nullable = string.Equals(Val(el, "Nullable"), "true", StringComparison.OrdinalIgnoreCase);
        var multiValue = string.Equals(Val(el, "MultiValue"), "true", StringComparison.OrdinalIgnoreCase);

        object? defaultValue = null;
        var defaultRaw = El(El(El(el, "DefaultValue"), "Values"), "Value")?.Value;
        if (!string.IsNullOrEmpty(defaultRaw) && !RdlExpression.IsExpression(defaultRaw))
        {
            try { defaultValue = System.Convert.ChangeType(defaultRaw, type, Inv); }
            catch (FormatException) { }
            catch (InvalidCastException) { }
        }

        ParameterAvailableValues? available = ReadAvailableValues(El(el, "ValidValues"));

        return new ReportParameter(name, type, prompt, defaultValue, multiValue, Required: !nullable, available);
    }

    private static ParameterAvailableValues? ReadAvailableValues(XElement? validValues)
    {
        if (validValues is null)
        {
            return null;
        }
        // Query-driven: <DataSetReference><DataSetName/><ValueField/><LabelField/>.
        var dsRef = El(validValues, "DataSetReference");
        if (dsRef is not null && Val(dsRef, "DataSetName") is { Length: > 0 } dsName)
        {
            return ParameterAvailableValues.FromQuery(dsName, Val(dsRef, "ValueField") ?? string.Empty, Val(dsRef, "LabelField"));
        }
        // Static: <ParameterValues><ParameterValue><Value/><Label/>.
        var statics = El(validValues, "ParameterValues")?.Elements()
            .Where(e => e.Name.LocalName == "ParameterValue")
            .Select(pv => new ParameterValue(Val(pv, "Value") ?? string.Empty, Val(pv, "Label")))
            .ToArray();
        return statics is { Length: > 0 }
            ? new ParameterAvailableValues { Values = new EquatableArray<ParameterValue>(statics) }
            : null;
    }

    private static Type MapType(string? dataType) => dataType switch
    {
        "Integer" => typeof(int),
        "Float" => typeof(double),
        "Decimal" => typeof(decimal),
        "Boolean" => typeof(bool),
        "DateTime" => typeof(DateTime),
        _ => typeof(string),
    };

    // ── XML + size helpers (namespace-agnostic via LocalName) ─────────────────────

    private static XElement? El(XElement? parent, string name)
        => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string? Val(XElement? parent, string name) => El(parent, name)?.Value;

    private static Rectangle Bounds(XElement item, Unit dx, Unit dy)
    {
        var left = (ParseSize(Val(item, "Left")) ?? Unit.Zero) + dx;
        var top = (ParseSize(Val(item, "Top")) ?? Unit.Zero) + dy;
        var width = ParseSize(Val(item, "Width")) ?? Unit.FromMm(25);
        var height = ParseSize(Val(item, "Height")) ?? Unit.FromMm(6);
        return new Rectangle(left, top, width, height);
    }

    private static Unit BoundsHeight(IEnumerable<ReportElement> elements)
    {
        Unit max = Unit.Zero;
        foreach (var e in elements)
        {
            if (e.Bounds.Bottom > max) { max = e.Bounds.Bottom; }
        }
        return max;
    }

    /// <summary>Parses an RDL size string (e.g. "2.5in", "21cm", "10mm", "20pt", "96px") into a
    /// <see cref="Unit"/>. Returns null when the input is empty/unparseable; defaults to mm when the
    /// number carries no unit suffix.</summary>
    internal static Unit? ParseSize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var s = raw.Trim();
        int i = 0;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or '-' or '+')) { i++; }
        if (i == 0 || !double.TryParse(s[..i], NumberStyles.Float, Inv, out var value))
        {
            return null;
        }
        var unit = s[i..].Trim().ToLowerInvariant();
        return unit switch
        {
            "in" => Unit.FromInch(value),
            "cm" => Unit.FromCm(value),
            "mm" or "" => Unit.FromMm(value),
            "pt" => Unit.FromPoint(value),
            "pc" => Unit.FromPoint(value * 12.0),
            "px" => Unit.FromPixels(value),
            _ => Unit.FromMm(value),
        };
    }
}
