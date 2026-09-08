using Reporting.Elements;
namespace Reporting.Designer.Blazor.ViewModels;

internal sealed class ElementSnapshotCommand(ElementViewModel element, ReportElement before, ReportElement after) : IDesignerCommand
{
    public string Description => "Editar propriedades";
    public void Execute() => element.LoadFrom(after);
    public void Undo() => element.LoadFrom(before);
}
