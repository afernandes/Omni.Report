using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>LIFO history of executed commands. Limit defaults to 100 — the oldest entries
/// drop off the bottom when full.</summary>
public sealed class CommandHistory : Notifying
{
    private readonly Stack<IDesignerCommand> _undo = new();
    private readonly Stack<IDesignerCommand> _redo = new();

    public int Limit { get; init; } = 100;
    public long Revision { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public IDesignerCommand? PeekUndo => _undo.Count > 0 ? _undo.Peek() : null;
    public IDesignerCommand? PeekRedo => _redo.Count > 0 ? _redo.Peek() : null;

    public void Push(IDesignerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        RecordExecuted(command);
    }

    internal void RecordExecuted(IDesignerCommand command)
    {
        Revision++;
        _undo.Push(command);
        _redo.Clear();
        Trim();
        RaiseChanged();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        var c = _undo.Pop();
        Revision++;
        c.Undo();
        _redo.Push(c);
        RaiseChanged();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        var c = _redo.Pop();
        Revision++;
        c.Execute();
        _undo.Push(c);
        RaiseChanged();
        return true;
    }

    public void Clear()
    {
        Revision++;
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
