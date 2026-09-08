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
