using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using Reporting.Geometry;
using Reporting.Styling;
using GdiColor = System.Drawing.Color;
using GdiFontStyle = System.Drawing.FontStyle;
using GdiRectangleF = System.Drawing.RectangleF;
using ReportingColor = Reporting.Styling.Color;
using ReportingFontStyle = Reporting.Styling.FontStyle;

namespace Reporting.Rendering.Gdi;

/// <summary>Conversions between OmniReport's device-independent types and GDI+ types.</summary>
[SupportedOSPlatform("windows")]
internal static class GdiConversions
{
    /// <summary>Converts a <see cref="Unit"/> (mils) to pixels at the supplied DPI.</summary>
    public static float Px(this Unit unit, float dpi)
        => unit.Mils * dpi / 1000f;

    public static GdiRectangleF ToRectF(this Reporting.Geometry.Rectangle r, float dpi)
        => new(r.X.Px(dpi), r.Y.Px(dpi), r.Width.Px(dpi), r.Height.Px(dpi));

    public static PointF ToPointF(this Reporting.Geometry.Point p, float dpi)
        => new(p.X.Px(dpi), p.Y.Px(dpi));

    public static GdiColor ToGdiColor(this ReportingColor c)
        => GdiColor.FromArgb(c.A, c.R, c.G, c.B);

    public static GdiFontStyle ToGdiFontStyle(this ReportingFontStyle style)
    {
        var result = GdiFontStyle.Regular;
        if ((style & ReportingFontStyle.Bold) != 0) result |= GdiFontStyle.Bold;
        if ((style & ReportingFontStyle.Italic) != 0) result |= GdiFontStyle.Italic;
        if ((style & ReportingFontStyle.Underline) != 0) result |= GdiFontStyle.Underline;
        if ((style & ReportingFontStyle.Strikeout) != 0) result |= GdiFontStyle.Strikeout;
        return result;
    }

    public static DashStyle ToDashStyle(this BorderLineStyle style)
        => style switch
        {
            BorderLineStyle.Dashed => DashStyle.Dash,
            BorderLineStyle.Dotted => DashStyle.Dot,
            BorderLineStyle.DashDot => DashStyle.DashDot,
            _ => DashStyle.Solid,
        };

    public static StringAlignment ToStringAlignment(this HorizontalAlignment alignment)
        => alignment switch
        {
            HorizontalAlignment.Center => StringAlignment.Center,
            HorizontalAlignment.Right => StringAlignment.Far,
            _ => StringAlignment.Near,
        };

    public static StringAlignment ToStringAlignment(this VerticalAlignment alignment)
        => alignment switch
        {
            VerticalAlignment.Middle => StringAlignment.Center,
            VerticalAlignment.Bottom => StringAlignment.Far,
            _ => StringAlignment.Near,
        };
}
