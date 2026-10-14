using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering;

/// <summary>Stroke style for outlines and lines.</summary>
/// <param name="Color">Stroke colour.</param>
/// <param name="Thickness">Stroke width.</param>
/// <param name="Style">Dash pattern; <see cref="BorderLineStyle.None"/> makes the stroke invisible.</param>
public sealed record PenStyle(
    Color Color,
    Unit Thickness,
    BorderLineStyle Style = BorderLineStyle.Solid)
{
    /// <summary>Black hairline, 0.5pt — the default stroke for lines and borders.</summary>
    public static readonly PenStyle Default = new(Color.Black, Unit.FromPoint(0.5));

    /// <summary>Black 0.25pt — the thinnest stroke that still prints reliably.</summary>
    public static readonly PenStyle Thin = new(Color.Black, Unit.FromPoint(0.25));

    /// <summary>True when the stroke would actually draw: it needs both a style and a non-zero thickness.</summary>
    public bool IsVisible => Style != BorderLineStyle.None && Thickness > Unit.Zero;

    /// <summary>Converts a border edge into a stroke, or null when the edge is invisible — so callers can
    /// pass the result straight to a draw call that treats null as "skip the outline".</summary>
    public static PenStyle? FromBorderSide(BorderSide side)
        => side.IsVisible ? new PenStyle(side.Color, side.Thickness, side.Style) : null;
}
