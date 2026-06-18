using System.Runtime.CompilerServices;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Minimal observable base — Razor components subscribe to <see cref="Changed"/> to know
/// when to re-render. Lighter than <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// (no property names — coarse "anything changed" signal). Sufficient for designer
/// state where the property grid and canvas re-render together anyway.
/// </summary>
public abstract class Notifying
{
    public event Action? Changed;

    protected void RaiseChanged() => Changed?.Invoke();

    /// <summary>Helper for properties: sets the backing field, raises the event if the value
    /// actually changed, returns true on change.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        RaiseChanged();
        return true;
    }
}
