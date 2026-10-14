using System.Globalization;
using Reporting.Aggregates;

namespace Reporting.Expressions;

/// <summary>Read-only string-keyed value lookup. Returns <c>null</c> for unknown keys.</summary>
public interface IValueLookup
{
    /// <summary>The value stored under <paramref name="key"/>, or null when the key is unknown. Never throws:
    /// an expression referencing a field that does not exist evaluates to null rather than killing the render.</summary>
    object? this[string key] { get; }

    /// <summary>Whether the key exists — the way to tell "absent" from "present but null".</summary>
    bool Contains(string key);

    /// <summary>Every available key. Used by the Designer to offer completion.</summary>
    IEnumerable<string> Keys { get; }
}
