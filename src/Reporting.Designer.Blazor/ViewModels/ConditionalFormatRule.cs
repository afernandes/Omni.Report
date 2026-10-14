using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>A single conditional formatting rule on an element. Mutable wrapper around
/// <see cref="ConditionalFormat"/>.</summary>
public sealed class ConditionalFormatRule : Notifying
{
    private string _condition = "Fields.Total > 1000";
    public string Condition { get => _condition; set => Set(ref _condition, value); }

    private Color? _foreColor;
    public Color? ForeColor { get => _foreColor; set => Set(ref _foreColor, value); }

    private Color? _backColor;
    public Color? BackColor { get => _backColor; set => Set(ref _backColor, value); }

    private Color? _backColorEnd;
    /// <summary>Gradient end colour for the conditional fill (start = <see cref="BackColor"/>).</summary>
    public Color? BackColorEnd { get => _backColorEnd; set => Set(ref _backColorEnd, value); }

    private BackgroundGradientType _backgroundGradient = BackgroundGradientType.None;
    /// <summary>Conditional fill gradient direction (None = solid). StyleResolver.Merge applies it on match.</summary>
    public BackgroundGradientType BackgroundGradient { get => _backgroundGradient; set => Set(ref _backgroundGradient, value); }

    private bool _bold;
    public bool Bold { get => _bold; set => Set(ref _bold, value); }

    private bool _italic;
    public bool Italic { get => _italic; set => Set(ref _italic, value); }

    internal ConditionalFormat ToConditionalFormat()
    {
        var fontStyle = FontStyle.Regular;
        if (Bold) fontStyle |= FontStyle.Bold;
        if (Italic) fontStyle |= FontStyle.Italic;
        var style = new Style(
            Font: fontStyle == FontStyle.Regular ? null : new Font("Arial", 10, fontStyle),
            ForeColor: ForeColor,
            BackColor: BackColor,
            BackColorEnd: BackColorEnd,
            BackgroundGradient: BackgroundGradient);
        return new ConditionalFormat(Condition, style);
    }

    internal static ConditionalFormatRule From(ConditionalFormat cf)
    {
        var rule = new ConditionalFormatRule
        {
            Condition = cf.Condition,
            ForeColor = cf.Style.ForeColor,
            BackColor = cf.Style.BackColor,
            BackColorEnd = cf.Style.BackColorEnd,
            BackgroundGradient = cf.Style.BackgroundGradient,
        };
        if (cf.Style.Font is { } f)
        {
            rule.Bold = (f.Style & FontStyle.Bold) != 0;
            rule.Italic = (f.Style & FontStyle.Italic) != 0;
        }
        return rule;
    }
}
