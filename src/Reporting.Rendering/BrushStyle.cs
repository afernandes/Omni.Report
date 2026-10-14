using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering;

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
