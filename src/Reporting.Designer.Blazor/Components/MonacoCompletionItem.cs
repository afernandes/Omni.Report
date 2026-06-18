namespace Reporting.Designer.Blazor.Components;

/// <summary>One IntelliSense suggestion the Monaco editor will offer the user.
/// Kept as a small DTO so it serializes cleanly across the JS interop boundary.</summary>
public sealed class MonacoCompletionItem
{
    /// <summary>Display label in the completion popup.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>One of <c>"field"</c>, <c>"parameter"</c>, <c>"function"</c>, <c>"keyword"</c>, <c>"snippet"</c>.</summary>
    public string Kind { get; set; } = "text";

    /// <summary>Text inserted when the item is accepted. For snippets, may include
    /// placeholders like <c>${1:field}</c>; set <see cref="Snippet"/> to <c>true</c> in that case.</summary>
    public string InsertText { get; set; } = string.Empty;

    /// <summary>Optional secondary detail (e.g. return type).</summary>
    public string? Detail { get; set; }

    /// <summary>Optional documentation shown in the hover panel.</summary>
    public string? Documentation { get; set; }

    /// <summary>True when <see cref="InsertText"/> uses Monaco snippet syntax (<c>${1:arg}</c>).</summary>
    public bool Snippet { get; set; }
}
