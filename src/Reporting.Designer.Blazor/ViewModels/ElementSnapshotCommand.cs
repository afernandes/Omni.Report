using Reporting.Elements;
namespace Reporting.Designer.Blazor.ViewModels;

internal sealed class ElementSnapshotCommand(ElementViewModel element, ReportElement before, ReportElement after,
    bool? beforeLocked = null, bool? afterLocked = null) : IDesignerCommand
{
    public string Description => "Editar propriedades";
    public void Execute()
    {
        element.LoadFrom(after);
        if (afterLocked is { } locked) element.IsLocked = locked;
    }
    public void Undo()
    {
        element.LoadFrom(before);
        if (beforeLocked is { } locked) element.IsLocked = locked;
    }
}
