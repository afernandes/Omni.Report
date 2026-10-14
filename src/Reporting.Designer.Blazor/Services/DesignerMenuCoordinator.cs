namespace Reporting.Designer.Blazor.Services;

/// <summary>Coordinates mutually exclusive menus within one designer, without static circuit state.</summary>
public sealed class DesignerMenuCoordinator
{
    /// <summary>Raised before opening a sibling menu.</summary>
    public event Action? CloseRequested;

    /// <summary>Whether a menu owned by this coordinator is open.</summary>
    public bool AnyOpen { get; private set; }

    /// <summary>Closes siblings and marks the new menu open.</summary>
    public void Open()
    {
        CloseRequested?.Invoke();
        AnyOpen = true;
    }

    /// <summary>Marks the current menu closed.</summary>
    public void Close() => AnyOpen = false;
}
