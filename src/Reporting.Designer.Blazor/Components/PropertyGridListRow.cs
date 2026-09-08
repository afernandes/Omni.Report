namespace Reporting.Designer.Blazor.Components;

internal sealed class PropertyGridListRow
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public object Item { get; set; } = default!;
    }
