using System.Globalization;
using System.Text.Json;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;

namespace Reporting.Output.Json;

/// <summary>
/// Dumps a <see cref="RenderedReport"/> as structured JSON — each page becomes an object
/// with its physical size and a flat list of positioned primitives (texts, lines, rects,
/// ellipses, images).
/// </summary>
/// <remarks>
/// <para>Use cases:</para>
/// <list type="bullet">
/// <item><b>Snapshot tests</b> — diff-friendly, deterministic output for regression suites.</item>
/// <item><b>Automation</b> — downstream pipelines (Power BI, Tableau, ETL) consume primitives directly.</item>
/// <item><b>LLM / RAG ingestion</b> — semantic structure beats OCR'd PNGs every time.</item>
/// <item><b>Debugging</b> — quick "what's actually on the page?" view without opening any viewer.</item>
/// </list>
///
/// <para>The schema is intentionally flat and stable: each primitive has a <c>type</c>
/// discriminator and the same <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> bounding-box fields.
/// Units are configurable via <see cref="JsonExportOptions.Unit"/>.</para>
/// </remarks>
public sealed class JsonExporter : IReportExporter
{
    private readonly JsonExportOptions _options;

    public JsonExporter(JsonExportOptions? options = null)
    {
        _options = options ?? JsonExportOptions.Default;
    }

    public string Format => "json";
    public string FileExtension => ".json";
    public string ContentType => "application/json; charset=utf-8";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Indented = _options.Indented,
            SkipValidation = false,
            // Default escaping is strict; reports may contain accented chars (R$, ç, ã).
            // Use Default encoder — it escapes only HTML-sensitive chars, leaving Unicode intact.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        writer.WriteStartObject();
        writer.WriteString("name", report.Name);
        writer.WriteNumber("pageCount", report.PageCount);
        writer.WriteString("unit", _options.Unit.ToString().ToLowerInvariant());

        writer.WritePropertyName("pages");
        writer.WriteStartArray();
        foreach (var page in report.Pages)
        {
            WritePage(writer, page);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();
    }

    private void WritePage(Utf8JsonWriter writer, RenderedPage page)
    {
        writer.WriteStartObject();
        writer.WriteNumber("pageNumber", page.PageNumber);

        writer.WritePropertyName("size");
        writer.WriteStartObject();
        WriteUnit(writer, "width", page.PageSetup.PageWidth);
        WriteUnit(writer, "height", page.PageSetup.PageHeight);
        writer.WriteEndObject();

        if (_options.TextsOnly)
        {
            writer.WritePropertyName("texts");
            writer.WriteStartArray();
            foreach (var t in page.Primitives.OfType<DrawTextPrimitive>())
            {
                WriteTextPrimitive(writer, t);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WritePropertyName("primitives");
            writer.WriteStartArray();
            foreach (var primitive in page.Primitives)
            {
                WritePrimitive(writer, primitive);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private void WritePrimitive(Utf8JsonWriter writer, LayoutPrimitive primitive)
    {
        switch (primitive)
        {
            case DrawTextPrimitive t:
                WriteTextPrimitive(writer, t);
                break;
            case DrawLinePrimitive l:
                writer.WriteStartObject();
                writer.WriteString("type", "line");
                WriteBounds(writer, l.Bounds);
                writer.WritePropertyName("from");
                WritePoint(writer, l.From);
                writer.WritePropertyName("to");
                WritePoint(writer, l.To);
                if (l.Pen is not null)
                {
                    writer.WriteString("color", FormatColor(l.Pen.Color));
                    WriteUnit(writer, "thickness", l.Pen.Thickness);
                }
                writer.WriteEndObject();
                break;
            case DrawRectanglePrimitive r:
                writer.WriteStartObject();
                writer.WriteString("type", "rect");
                WriteBounds(writer, r.Bounds);
                if (r.Pen is not null)
                {
                    writer.WriteString("strokeColor", FormatColor(r.Pen.Color));
                    WriteUnit(writer, "strokeThickness", r.Pen.Thickness);
                }
                if (r.Fill is not null)
                {
                    writer.WriteString("fillColor", FormatColor(r.Fill.Color));
                }
                writer.WriteEndObject();
                break;
            case DrawEllipsePrimitive e:
                writer.WriteStartObject();
                writer.WriteString("type", "ellipse");
                WriteBounds(writer, e.Bounds);
                if (e.Pen is not null)
                {
                    writer.WriteString("strokeColor", FormatColor(e.Pen.Color));
                    WriteUnit(writer, "strokeThickness", e.Pen.Thickness);
                }
                if (e.Fill is not null)
                {
                    writer.WriteString("fillColor", FormatColor(e.Fill.Color));
                }
                writer.WriteEndObject();
                break;
            case DrawImagePrimitive i:
                writer.WriteStartObject();
                writer.WriteString("type", "image");
                WriteBounds(writer, i.Bounds);
                writer.WriteNumber("byteCount", i.Data.Count);
                writer.WriteEndObject();
                break;
            case DrawPolygonPrimitive poly:
                writer.WriteStartObject();
                writer.WriteString("type", "polygon");
                WriteBounds(writer, poly.Bounds);
                writer.WriteBoolean("closed", poly.Closed);
                if (poly.Pen is not null)
                {
                    writer.WriteString("strokeColor", FormatColor(poly.Pen.Color));
                    WriteUnit(writer, "strokeThickness", poly.Pen.Thickness);
                }
                if (poly.Fill is not null)
                {
                    writer.WriteString("fillColor", FormatColor(poly.Fill.Color));
                }
                writer.WritePropertyName("points");
                writer.WriteStartArray();
                foreach (var pt in poly.Points)
                {
                    WritePoint(writer, pt);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
                break;
        }
    }

    private void WriteTextPrimitive(Utf8JsonWriter writer, DrawTextPrimitive t)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        WriteBounds(writer, t.Bounds);
        writer.WriteString("text", t.Text);
        if (_options.IncludeStyles && t.Style is not null)
        {
            writer.WritePropertyName("style");
            writer.WriteStartObject();
            writer.WriteString("fontFamily", t.Style.Font.Family);
            writer.WriteNumber("fontSize", t.Style.Font.Size);
            if (t.Style.Font.Style != Reporting.Styling.FontStyle.Regular)
            {
                writer.WriteString("fontStyle", t.Style.Font.Style.ToString());
            }
            writer.WriteString("color", FormatColor(t.Style.ForeColor));
            if (t.Style.HorizontalAlignment != Reporting.Styling.HorizontalAlignment.Left)
            {
                writer.WriteString("horizontalAlignment", t.Style.HorizontalAlignment.ToString());
            }
            if (t.Style.VerticalAlignment != Reporting.Styling.VerticalAlignment.Top)
            {
                writer.WriteString("verticalAlignment", t.Style.VerticalAlignment.ToString());
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private void WriteBounds(Utf8JsonWriter writer, Rectangle bounds)
    {
        WriteUnit(writer, "x", bounds.X);
        WriteUnit(writer, "y", bounds.Y);
        WriteUnit(writer, "width", bounds.Width);
        WriteUnit(writer, "height", bounds.Height);
    }

    private void WritePoint(Utf8JsonWriter writer, Point pt)
    {
        writer.WriteStartObject();
        WriteUnit(writer, "x", pt.X);
        WriteUnit(writer, "y", pt.Y);
        writer.WriteEndObject();
    }

    private void WriteUnit(Utf8JsonWriter writer, string propertyName, Unit value)
    {
        switch (_options.Unit)
        {
            case JsonUnit.Mils:
                writer.WriteNumber(propertyName, value.Mils);
                break;
            case JsonUnit.Points:
                writer.WriteNumber(propertyName, Math.Round(value.ToPoints(), 3));
                break;
            case JsonUnit.Millimeters:
            default:
                // Unit doesn't expose ToMillimeters() directly; convert via points.
                var mm = value.ToPoints() * 25.4 / 72.0;
                writer.WriteNumber(propertyName, Math.Round(mm, 3));
                break;
        }
    }

    private static string FormatColor(Reporting.Styling.Color c)
    {
        // Emit as #AARRGGBB when alpha is not 255, otherwise compact #RRGGBB.
        if (c.A == 255)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
        return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
    }
}
