using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

public sealed class ResizeElementCommand : IDesignerCommand
{
    private readonly ElementViewModel _element;
    private readonly Unit _oldW, _oldH, _newW, _newH;

    public ResizeElementCommand(ElementViewModel element, Unit newWidth, Unit newHeight)
    {
        _element = element;
        _oldW = element.Width;
        _oldH = element.Height;
        _newW = newWidth;
        _newH = newHeight;
    }

    public string Description => "Resize element";

    public void Execute() { _element.Width = _newW; _element.Height = _newH; }
    public void Undo() { _element.Width = _oldW; _element.Height = _oldH; }
}
