using Microsoft.AspNetCore.Components;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Where keyboard focus goes back to when a Designer modal closes. Shortcuts are handled on the Designer
/// root, so focus left on <c>&lt;body&gt;</c> after a dialog closes would silence them until the next click. Modals
/// stack (a dialog can open another), so closing one returns focus to the one beneath it, and the last to the root.</summary>
internal sealed class DesignerFocusScope
{
    private readonly List<object> _openModals = [];
    private readonly Dictionary<object, ElementReference> _dialogs = [];

    /// <summary>The Designer root (<c>.report-designer</c>), set after its first render.</summary>
    internal ElementReference? Root { get; set; }

    /// <summary>Whether any modal is open — canvas shortcuts must not act on the report behind it.</summary>
    internal bool HasOpenModal => _openModals.Count > 0;

    internal void Opened(object modal, ElementReference dialog)
    {
        _openModals.Remove(modal);
        _openModals.Add(modal);
        _dialogs[modal] = dialog;
    }

    /// <summary>Forgets <paramref name="modal"/> and returns what should get focus now.</summary>
    internal ElementReference? Closed(object modal)
    {
        _openModals.Remove(modal);
        _dialogs.Remove(modal);
        return _openModals.Count > 0 ? _dialogs[_openModals[^1]] : Root;
    }
}
