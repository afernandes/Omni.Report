using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Command pattern over the designer state — every user mutation goes through
/// a command, enabling undo/redo and (later) collaborative editing.</summary>
public interface IDesignerCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>LIFO history of executed commands. Limit defaults to 100 — the oldest entries
/// drop off the bottom when full.</summary>
public sealed class CommandHistory : Notifying
{
    private readonly Stack<IDesignerCommand> _undo = new();
    private readonly Stack<IDesignerCommand> _redo = new();

    public int Limit { get; init; } = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public IDesignerCommand? PeekUndo => _undo.Count > 0 ? _undo.Peek() : null;
    public IDesignerCommand? PeekRedo => _redo.Count > 0 ? _redo.Peek() : null;

    public void Push(IDesignerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        Trim();
        RaiseChanged();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        var c = _undo.Pop();
        c.Undo();
        _redo.Push(c);
        RaiseChanged();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        var c = _redo.Pop();
        c.Execute();
        _undo.Push(c);
        RaiseChanged();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        RaiseChanged();
    }

    private void Trim()
    {
        if (_undo.Count <= Limit) return;
        // Re-build keeping the most recent `Limit` entries.
        var keep = _undo.Take(Limit).ToArray();
        _undo.Clear();
        for (int i = keep.Length - 1; i >= 0; i--)
        {
            _undo.Push(keep[i]);
        }
    }
}

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

public sealed class RemoveElementCommand : IDesignerCommand
{
    private readonly BandViewModel _band;
    private readonly ElementViewModel _element;

    public RemoveElementCommand(BandViewModel band, ElementViewModel element)
    {
        _band = band;
        _element = element;
    }

    public string Description => $"Remove {_element.Kind}";

    public void Execute() => _band.RemoveElement(_element);
    public void Undo() => _band.AddElement(_element);
}

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

public sealed class ChangePropertyCommand<T> : IDesignerCommand
{
    private readonly Func<T> _get;
    private readonly Action<T> _set;
    private readonly T _newValue;
    private T _oldValue;

    public ChangePropertyCommand(string description, Func<T> getter, Action<T> setter, T newValue)
    {
        Description = description;
        _get = getter;
        _set = setter;
        _newValue = newValue;
        _oldValue = getter();
    }

    public string Description { get; }

    public void Execute()
    {
        _oldValue = _get();
        _set(_newValue);
    }

    public void Undo() => _set(_oldValue);
}
