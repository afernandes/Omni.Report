namespace Reporting.Designer.Blazor.Components;

internal sealed class PropertyGridDictRow
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
