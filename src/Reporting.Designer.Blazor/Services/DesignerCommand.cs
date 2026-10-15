namespace Reporting.Designer.Blazor.Services;

/// <summary>One action of the Designer. The same list drives the menus, the command palette and the shortcuts dialog,
/// so an action cannot exist in one of them and be missing — or advertise a different shortcut — in another.</summary>
/// <param name="Id">Command id routed by the Designer's dispatcher (e.g. <c>edit.undo</c>, <c>file.export.pdf</c>).</param>
/// <param name="Title">Label shown to the author.</param>
/// <param name="Menu">Top-level menu that lists the command (e.g. <c>Editar</c>).</param>
/// <param name="Icon">Icon name from the Designer's icon catalog.</param>
/// <param name="Shortcut">Keyboard shortcut that actually triggers the command, or null when there is none.</param>
/// <param name="StartsGroup">True when a separator precedes the command in its menu.</param>
public sealed record DesignerCommand(string Id, string Title, string Menu, string Icon, string? Shortcut = null,
    bool StartsGroup = false);
