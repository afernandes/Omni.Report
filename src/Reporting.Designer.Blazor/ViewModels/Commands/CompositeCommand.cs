using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Groups several commands into ONE undoable unit — e.g. a multi-element align / distribute /
/// same-size operation that must revert in a single Ctrl+Z. Execute applies them in order; Undo reverts
/// in reverse order. Pushing this is how multi-element toolbar operations stay undoable.</summary>
public sealed class CompositeCommand : IDesignerCommand
{
    private readonly IReadOnlyList<IDesignerCommand> _commands;

    public CompositeCommand(string description, IReadOnlyList<IDesignerCommand> commands)
    {
        Description = description;
        _commands = commands;
    }

    public string Description { get; }

    public void Execute()
    {
        foreach (var c in _commands)
        {
        c.Execute();
        }
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
