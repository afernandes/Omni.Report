namespace Reporting.Expressions;

internal sealed class GroupScopeSelection(Action restore) : IDisposable
{
    private Action? _restore = restore;

    public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
}
