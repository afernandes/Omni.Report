using System.Globalization;
using System.Xml;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;

namespace Reporting.Output.Xml;

/// <summary>
/// Dumps a <see cref="RenderedReport"/> as structured XML — each page becomes a <c>&lt;page&gt;</c> with its
/// physical size and a flat list of positioned primitives (texts, lines, rects, ellipses, images, polygons).
/// The data-oriented analogue of SSRS's XML rendering extension; mirrors the JSON exporter's schema as XML.
/// </summary>
public sealed class XmlExporter : IReportExporter
{
    private readonly XmlExportOptions _options;

    public XmlExporter(XmlExportOptions? options = null) => _options = options ?? XmlExportOptions.Default;

    public string Format => "xml";
    public string FileExtension => ".xml";
    public string ContentType => "application/xml; charset=utf-8";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        using var writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            Indent = _options.Indented,
            IndentChars = "  ",
            Encoding = new System.Text.UTF8Encoding(false), // no BOM
            CloseOutput = false,
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("report");
        writer.WriteAttributeString("name", report.Name);
        writer.WriteAttributeString("pageCount", report.PageCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("unit", _options.Unit.ToString().ToLowerInvariant());

        writer.WriteStartElement("pages");
        foreach (var page in report.Pages)
        {
            WritePage(writer, page);
        }
        writer.WriteEndElement(); // pages

        writer.WriteEndElement(); // report
        writer.WriteEndDocument();
        writer.Flush();
    }

    private void WritePage(XmlWriter writer, RenderedPage page)
    {
        writer.WriteStartElement("page");
        writer.WriteAttributeString("pageNumber", page.PageNumber.ToString(CultureInfo.InvariantCulture));

        writer.WriteStartElement("size");
        WriteUnitAttr(writer, "width", page.PageSetup.PageWidth);
        WriteUnitAttr(writer, "height", page.PageSetup.PageHeight);
        writer.WriteEndElement();

        writer.WriteStartElement("primitives");
        if (_options.TextsOnly)
        {
            foreach (var t in page.Primitives.OfType<DrawTextPrimitive>())
            {
                WriteText(writer, t);
            }
        }
        else
        {
            foreach (var primitive in page.Primitives)
            {
                WritePrimitive(writer, primitive);
            }
        }
        writer.WriteEndElement(); // primitives

        writer.WriteEndElement(); // page
    }

    private void WritePrimitive(XmlWriter writer, LayoutPrimitive primitive)
    {
        switch (primitive)
        {
            case DrawTextPrimitive t:
                WriteText(writer, t);
                break;
            case DrawLinePrimitive l:
                writer.WriteStartElement("line");
                WriteBounds(writer, l.Bounds);
                WriteUnitAttr(writer, "fromX", l.From.X);
                WriteUnitAttr(writer, "fromY", l.From.Y);
                WriteUnitAttr(writer, "toX", l.To.X);
                WriteUnitAttr(writer, "toY", l.To.Y);
                if (l.Pen is not null)
                {
                    writer.WriteAttributeString("color", FormatColor(l.Pen.Color));
                    WriteUnitAttr(writer, "thickness", l.Pen.Thickness);
                }
                writer.WriteEndElement();
                break;
            case DrawRectanglePrimitive r:
                writer.WriteStartElement("rect");
                WriteBounds(writer, r.Bounds);
                if (r.Pen is not null)
                {
                    writer.WriteAttributeString("strokeColor", FormatColor(r.Pen.Color));
                    WriteUnitAttr(writer, "strokeThickness", r.Pen.Thickness);
                }
                if (r.Fill is not null)
                {
                    writer.WriteAttributeString("fillColor", FormatColor(r.Fill.Color));
                }
                writer.WriteEndElement();
                break;
            case DrawEllipsePrimitive e:
                writer.WriteStartElement("ellipse");
                WriteBounds(writer, e.Bounds);
                if (e.Pen is not null)
                {
                    writer.WriteAttributeString("strokeColor", FormatColor(e.Pen.Color));
                    WriteUnitAttr(writer, "strokeThickness", e.Pen.Thickness);
                }
                if (e.Fill is not null)
                {
                    writer.WriteAttributeString("fillColor", FormatColor(e.Fill.Color));
                }
                writer.WriteEndElement();
                break;
            case DrawImagePrimitive i:
                writer.WriteStartElement("image");
                WriteBounds(writer, i.Bounds);
                writer.WriteAttributeString("byteCount", i.Data.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("sizing", i.Sizing.ToString());
                writer.WriteEndElement();
                break;
            case DrawPolygonPrimitive poly:
                writer.WriteStartElement("polygon");
                WriteBounds(writer, poly.Bounds);
                writer.WriteAttributeString("closed", poly.Closed ? "true" : "false");
                if (poly.Pen is not null)
                {
                    writer.WriteAttributeString("strokeColor", FormatColor(poly.Pen.Color));
                    WriteUnitAttr(writer, "strokeThickness", poly.Pen.Thickness);
                }
                if (poly.Fill is not null)
                {
                    writer.WriteAttributeString("fillColor", FormatColor(poly.Fill.Color));
                }
                foreach (var pt in poly.Points)
                {
                    writer.WriteStartElement("point");
                    WriteUnitAttr(writer, "x", pt.X);
                    WriteUnitAttr(writer, "y", pt.Y);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                break;
        }
    }

    private void WriteText(XmlWriter writer, DrawTextPrimitive t)
    {
        writer.WriteStartElement("text");
        WriteBounds(writer, t.Bounds);
        if (_options.IncludeStyles && t.Style is not null)
        {
            writer.WriteAttributeString("fontFamily", t.Style.Font.Family);
            writer.WriteAttributeString("fontSize", t.Style.Font.Size.ToString(CultureInfo.InvariantCulture));
            if (t.Style.Font.Style != Reporting.Styling.FontStyle.Regular)
            {
                writer.WriteAttributeString("fontStyle", t.Style.Font.Style.ToString());
            }
            writer.WriteAttributeString("color", FormatColor(t.Style.ForeColor));
            if (t.Style.HorizontalAlignment != Reporting.Styling.HorizontalAlignment.Left)
            {
                writer.WriteAttributeString("horizontalAlignment", t.Style.HorizontalAlignment.ToString());
            }
            if (t.Style.VerticalAlignment != Reporting.Styling.VerticalAlignment.Top)
            {
                writer.WriteAttributeString("verticalAlignment", t.Style.VerticalAlignment.ToString());
            }
        }
        writer.WriteString(t.Text ?? string.Empty); // element content; XmlWriter escapes special chars
        writer.WriteEndElement();
    }

    private void WriteBounds(XmlWriter writer, Rectangle bounds)
    {
        WriteUnitAttr(writer, "x", bounds.X);
        WriteUnitAttr(writer, "y", bounds.Y);
        WriteUnitAttr(writer, "width", bounds.Width);
        WriteUnitAttr(writer, "height", bounds.Height);
    }

    private void WriteUnitAttr(XmlWriter writer, string name, Unit value)
    {
        var v = _options.Unit switch
        {
            XmlUnit.Mils => value.Mils.ToString(CultureInfo.InvariantCulture),
            XmlUnit.Points => Math.Round(value.ToPoints(), 3).ToString(CultureInfo.InvariantCulture),
            _ => Math.Round(value.ToPoints() * 25.4 / 72.0, 3).ToString(CultureInfo.InvariantCulture), // mm
        };
        writer.WriteAttributeString(name, v);
    }

    private static string FormatColor(Reporting.Styling.Color c)
        => c.A == 255
            ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
            : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
}
