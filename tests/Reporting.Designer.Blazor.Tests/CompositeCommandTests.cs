using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// A multi-element toolbar operation (align / distribute / same-size) must be a SINGLE undoable unit.
/// The <see cref="CompositeCommand"/> groups the per-element moves/resizes so one Ctrl+Z reverts the
/// whole operation — before this, those operations bypassed history entirely and could not be undone.
/// </summary>
public class CompositeCommandTests
{
    [Fact]
    public void Groups_moves_and_resizes_into_one_undoable_unit()
    {
        var b = new ElementViewModel(DesignerElementKind.TextBox, "b")
        {
            X = Unit.FromMm(50), Y = Unit.FromMm(30), Width = Unit.FromMm(40), Height = Unit.FromMm(8),
        };

        // Build the commands BEFORE pushing — each captures the element's current position as "old".
        var composite = new CompositeCommand("Align Left + SameWidth", new IDesignerCommand[]
        {
            new MoveElementCommand(b, Unit.FromMm(10), b.Y),         // old X = 50
            new ResizeElementCommand(b, Unit.FromMm(20), b.Height),  // old W = 40
        });

        var history = new CommandHistory();
        history.Push(composite); // Push executes the command

        b.X.Should().Be(Unit.FromMm(10), "the move is applied");
        b.Width.Should().Be(Unit.FromMm(20), "the resize is applied");
        history.CanUndo.Should().BeTrue("the operation is recorded as one undoable unit");

        history.Undo();
        b.X.Should().Be(Unit.FromMm(50), "undo reverts the move…");
        b.Width.Should().Be(Unit.FromMm(40), "…and the resize, in a single Ctrl+Z");

        history.Redo();
        b.X.Should().Be(Unit.FromMm(10), "redo re-applies the whole operation");
        b.Width.Should().Be(Unit.FromMm(20));
    }

    [Fact]
    public void Executes_forward_and_undoes_in_reverse_order()
    {
        var log = new List<string>();
        var composite = new CompositeCommand("seq", new IDesignerCommand[]
        {
            new RecordingCommand(log, "A"),
            new RecordingCommand(log, "B"),
        });

        composite.Execute();
        composite.Undo();

        log.Should().Equal("exec:A", "exec:B", "undo:B", "undo:A");
    }

    private sealed class RecordingCommand : IDesignerCommand
    {
        private readonly List<string> _log;
        private readonly string _name;
        public RecordingCommand(List<string> log, string name) { _log = log; _name = name; }
        public string Description => _name;
        public void Execute() => _log.Add($"exec:{_name}");
        public void Undo() => _log.Add($"undo:{_name}");
    }
}
