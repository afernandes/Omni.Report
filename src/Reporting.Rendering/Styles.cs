using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering;

/// <summary>Text rendering style resolved from a domain <see cref="Style"/> + <see cref="Font"/>.</summary>
/// <param name="Font">Family, size and style attributes.</param>
/// <param name="ForeColor">Colour the glyphs are painted in.</param>
/// <param name="HorizontalAlignment">Placement across the box.</param>
/// <param name="VerticalAlignment">Placement down the box.</param>
/// <param name="WordWrap">Whether long text wraps to further lines instead of overflowing on one.</param>
/// <param name="Padding">Space between the box edges and the text.</param>
public sealed record TextStyle(
    Font Font,
    Color ForeColor,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true,
    Thickness Padding = default)
{
    /// <summary>Black text in the default font.</summary>
    public static readonly TextStyle Default = new(Font.Default, Color.Black);

    /// <summary>A copy using a different font.</summary>
    public TextStyle WithFont(Font font) => this with { Font = font };

    /// <summary>A copy using a different foreground colour.</summary>
    public TextStyle WithColor(Color color) => this with { ForeColor = color };
}

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

/// <summary>Fill style for shapes — solid, or a two-colour gradient. <see cref="Color"/> is the solid colour
/// (and the gradient start); when <see cref="Gradient"/> is not <see cref="BackgroundGradientType.None"/> and
/// <see cref="GradientEndColor"/> is set, the fill blends <see cref="Color"/> → <see cref="GradientEndColor"/>
/// along that direction. Optional params keep every existing <c>new BrushStyle(color)</c> call solid.</summary>
/// <param name="Color">Solid fill colour, and the start colour of a gradient.</param>
/// <param name="GradientEndColor">End colour of the gradient. Null means a solid fill.</param>
/// <param name="Gradient">Direction of the blend. <see cref="BackgroundGradientType.None"/> means solid.</param>
public sealed record BrushStyle(
    Color Color,
    Color? GradientEndColor = null,
    BackgroundGradientType Gradient = BackgroundGradientType.None)
{
    /// <summary>Opaque black fill.</summary>
    public static readonly BrushStyle Black = new(Color.Black);

    /// <summary>Opaque white fill.</summary>
    public static readonly BrushStyle White = new(Color.White);

    /// <summary>A fill that paints nothing.</summary>
    public static readonly BrushStyle Transparent = new(Color.Transparent);

    /// <summary>Visible when the solid/start colour OR (for a gradient) the end colour has any opacity.</summary>
    public bool IsVisible => Color.A > 0 || (HasGradient && GradientEndColor!.Value.A > 0);

    /// <summary>True when a real gradient should be drawn (direction set AND an end colour present).</summary>
    public bool HasGradient => Gradient != BackgroundGradientType.None && GradientEndColor is not null;
}
