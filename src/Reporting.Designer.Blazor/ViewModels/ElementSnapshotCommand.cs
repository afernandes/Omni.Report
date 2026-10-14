using Reporting.Elements;
namespace Reporting.Designer.Blazor.ViewModels;

internal sealed class ElementSnapshotCommand(ElementViewModel element, ReportElement before, ReportElement after,
    bool? beforeLocked = null, bool? afterLocked = null) : IDesignerCommand
{
    // Bloquear/ocultar produz snapshots de domínio idênticos (IsLocked vive só no VM). Reidratar com LoadFrom
    // nesse caso recriaria os VMs filhos de um Rectangle: perderia o bloqueio dos filhos e deixaria comandos
    // anteriores do histórico apontando para instâncias desanexadas.
    private readonly bool _domainChanged = !Equals(before, after);

    public string Description => "Editar propriedades";
    public void Execute()
    {
        if (_domainChanged) element.LoadFrom(after);
        if (afterLocked is { } locked) element.IsLocked = locked;
    }
    public void Undo()
    {
        if (_domainChanged) element.LoadFrom(before);
        if (beforeLocked is { } locked) element.IsLocked = locked;
    }
}
