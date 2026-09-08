using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

public sealed class RemoveElementCommand : IDesignerCommand
{
    private readonly BandViewModel _band;
    private readonly ElementViewModel _element;
    private readonly int _index;

    public RemoveElementCommand(BandViewModel band, ElementViewModel element)
    {
        _band = band;
        _element = element;
        _index = band.Elements.IndexOf(element);
    }

    public string Description => $"Remove {_element.Kind}";

    public void Execute() => _band.RemoveElement(_element);
    public void Undo()
    {
        _band.AddElement(_element);
        _band.Elements.Move(_band.Elements.Count - 1, Math.Clamp(_index, 0, _band.Elements.Count - 1));
    }
}
