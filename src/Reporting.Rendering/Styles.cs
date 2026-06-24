using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering;

/// <summary>Text rendering style resolved from a domain <see cref="Style"/> + <see cref="Font"/>.</summary>
public sealed record TextStyle(
    Font Font,
    Color ForeColor,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true,
    Thickness Padding = default)
{
    public static readonly TextStyle Default = new(Font.Default, Color.Black);

    public TextStyle WithFont(Font font) => this with { Font = font };
    public TextStyle WithColor(Color color) => this with { ForeColor = color };
}

/// <summary>Stroke style for outlines and lines.</summary>
public sealed record PenStyle(
    Color Color,
    Unit Thickness,
    BorderLineStyle Style = BorderLineStyle.Solid)
{
    public static readonly PenStyle Default = new(Color.Black, Unit.FromPoint(0.5));
    public static readonly PenStyle Thin = new(Color.Black, Unit.FromPoint(0.25));

    public bool IsVisible => Style != BorderLineStyle.None && Thickness > Unit.Zero;

    public static PenStyle? FromBorderSide(BorderSide side)
        => side.IsVisible ? new PenStyle(side.Color, side.Thickness, side.Style) : null;
}

/// <summary>Fill style for shapes — solid, or a two-colour gradient. <see cref="Color"/> is the solid colour
/// (and the gradient start); when <see cref="Gradient"/> is not <see cref="BackgroundGradientType.None"/> and
/// <see cref="GradientEndColor"/> is set, the fill blends <see cref="Color"/> → <see cref="GradientEndColor"/>
/// along that direction. Optional params keep every existing <c>new BrushStyle(color)</c> call solid.</summary>
public sealed record BrushStyle(
    Color Color,
    Color? GradientEndColor = null,
    BackgroundGradientType Gradient = BackgroundGradientType.None)
{
    public static readonly BrushStyle Black = new(Color.Black);
    public static readonly BrushStyle White = new(Color.White);
    public static readonly BrushStyle Transparent = new(Color.Transparent);

    public bool IsVisible => Color.A > 0;

    /// <summary>True when a real gradient should be drawn (direction set AND an end colour present).</summary>
    public bool HasGradient => Gradient != BackgroundGradientType.None && GradientEndColor is not null;
}
