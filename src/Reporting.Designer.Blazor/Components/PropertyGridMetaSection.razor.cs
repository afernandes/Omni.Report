using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
using System.Globalization;
using Reporting.Designer.Blazor.Services;
namespace Reporting.Designer.Blazor.Components;

public partial class PropertyGridMetaSection
{
    protected override ElementViewModel? EditedElement => Element;
    [Parameter, EditorRequired] public ElementViewModel Element { get; set; } = default!;

    /// <summary>Raised when the user clicks "fx" on a bindable property — the argument is the property
    /// path. The host opens the rich expression editor (Monaco) seeded with the current binding and
    /// writes the result back to <c>PropertyExpressions[path]</c>.</summary>
    [Parameter] public EventCallback<string> OnEditExpression { get; set; }

    private IReadOnlyList<PropertyGridDescriptor> _descriptors = Array.Empty<PropertyGridDescriptor>();

    protected override void OnParametersSet()
        => _descriptors = PropertyGridDescriptors.For(Element.ToElement().GetType());

    private static Type EnumType(PropertyGridDescriptor d) => Nullable.GetUnderlyingType(d.Type) ?? d.Type;

    private static double ParseDouble(object? value)
        => double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0;

    private static object ConvertNumber(PropertyGridDescriptor d, object? value)
        => Convert.ChangeType(ParseDouble(value), Nullable.GetUnderlyingType(d.Type) ?? d.Type, CultureInfo.InvariantCulture);

    // ── Format preset: a dropdown of common .NET format strings + a custom escape hatch.
    private static readonly HashSet<string> KnownFormats = new(StringComparer.Ordinal)
    {
        "C", "N0", "N2", "P", "dd/MM/yyyy", "HH:mm", "dd/MM/yyyy HH:mm",
    };

    // Paths whose format-preset dropdown is in "Custom…" mode — kept in local UI state so selecting Custom
    // opens the text input WITHOUT persisting a bogus literal format ("custom" was being written as the
    // .NET format string, corrupting the rendered value). Typing a real value persists it.
    private readonly HashSet<string> _customFormat = new();

    private void OnFormatPreset(PropertyGridDescriptor d, string? value)
    {
        if (value == "__custom__")
        {
            _customFormat.Add(d.Path);
            return;
        }
        _customFormat.Remove(d.Path);
        Element.ApplyMetaSet(d, string.IsNullOrEmpty(value) ? null : value);
    }

    // Custom format box: if the author types a string that IS a known preset, leave Custom mode so the dropdown
    // re-syncs to that preset (otherwise it stayed stuck on "Custom…").
    private void OnCustomFormatTyped(PropertyGridDescriptor d, string? value)
    {
        if (!string.IsNullOrEmpty(value) && KnownFormats.Contains(value))
        {
            _customFormat.Remove(d.Path);
        }
        Element.ApplyMetaSet(d, string.IsNullOrEmpty(value) ? null : value);
    }

    // Nullable string editors write null (not "") for an empty box — null = "inherit/absent" by convention.
    private static object? EmptyToNull(string? s) => string.IsNullOrEmpty(s) ? null : s;

    // Colour handling (RGB-strip for the input, alpha-preserve on write) lives in the shared ColorHexUtil,
    // so every colour editor stays in parity — see its remarks.

    // Show the first VISIBLE side so a non-uniform border (e.g. bottom-only, built code-first / loaded from
    // a file) doesn't read as "Nenhuma". Editing still applies uniformly to all four sides (see below).
    private static BorderSide? FirstVisibleSide(Border? b)
    {
        if (b is null)
        {
            return null;
        }
        foreach (var side in new[] { b.Top, b.Right, b.Bottom, b.Left })
        {
            if (side.Style != BorderLineStyle.None)
            {
                return side;
            }
        }
        return b.Top;
    }

    // ── Border / Padding: edit the box uniformly (all four sides the same), matching the previous
    //    hand-coded editors. A "None" style clears the border; a non-positive padding clears it.
    //    The base side is the SAME first-visible side the editor displays — using current.Top instead
    //    would, on a non-uniform border whose Top is None, rebuild every side as None and ERASE the border.
    private static Border? WithBorderStyle(Border? current, string? styleName)
    {
        if (string.IsNullOrEmpty(styleName) || !Enum.TryParse<BorderLineStyle>(styleName, out var style) || style == BorderLineStyle.None)
        {
            return null;
        }
        var side = (FirstVisibleSide(current) ?? new BorderSide(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black)) with { Style = style };
        return new Border(side, side, side, side);
    }

    private static Border WithBorderThickness(Border current, double points)
    {
        var side = (FirstVisibleSide(current) ?? current.Top) with { Thickness = Unit.FromPoint(points) };
        return new Border(side, side, side, side);
    }

    private static Border WithBorderColor(Border current, string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return current;
        }
        try
        {
            var baseSide = FirstVisibleSide(current) ?? current.Top;
            // Keep the existing alpha — the colour input only provides #RRGGBB.
            var side = baseSide with { Color = Color.FromHex(hex) with { A = baseSide.Color.A } };
            return new Border(side, side, side, side);
        }
        catch (FormatException)
        {
            return current;
        }
    }

    private static Thickness? PaddingFromMm(double mm)
    {
        if (mm <= 0)
        {
            return null;
        }
        var u = Unit.FromMm(mm);
        return new Thickness(u, u, u, u);
    }

}
