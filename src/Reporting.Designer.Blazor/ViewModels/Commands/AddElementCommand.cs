using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

// ── Concrete commands ──────────────────────────────────────────────────────────

public sealed class AddElementCommand : IDesignerCommand
{
    private readonly BandViewModel _band;
    private readonly ElementViewModel _element;

    public AddElementCommand(BandViewModel band, ElementViewModel element)
    {
        _band = band;
        _element = element;
    }

    public string Description => $"Add {_element.Kind}";

    public void Execute() => _band.AddElement(_element);
    public void Undo() => _band.RemoveElement(_element);
}
