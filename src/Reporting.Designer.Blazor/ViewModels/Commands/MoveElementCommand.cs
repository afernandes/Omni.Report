using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

public sealed class MoveElementCommand : IDesignerCommand
{
    private readonly ElementViewModel _element;
    private readonly Unit _oldX, _oldY, _newX, _newY;

    public MoveElementCommand(ElementViewModel element, Unit newX, Unit newY)
    {
        _element = element;
        _oldX = element.X;
        _oldY = element.Y;
        _newX = newX;
        _newY = newY;
    }

    public string Description => "Move element";

    public void Execute() { _element.X = _newX; _element.Y = _newY; }
    public void Undo() { _element.X = _oldX; _element.Y = _oldY; }
}
