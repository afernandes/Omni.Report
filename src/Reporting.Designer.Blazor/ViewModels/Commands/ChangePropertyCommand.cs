using Reporting.Geometry;
namespace Reporting.Designer.Blazor.ViewModels;

public sealed class ChangePropertyCommand<T> : IDesignerCommand
{
    private readonly Func<T> _get;
    private readonly Action<T> _set;
    private readonly T _newValue;
    private T _oldValue;

    public ChangePropertyCommand(string description, Func<T> getter, Action<T> setter, T newValue)
    {
        Description = description;
        _get = getter;
        _set = setter;
        _newValue = newValue;
        _oldValue = getter();
    }

    public string Description { get; }

    public void Execute()
    {
        _oldValue = _get();
        _set(_newValue);
    }

    public void Undo() => _set(_oldValue);
}
