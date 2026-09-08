namespace Reporting.Elements;

/// <summary>Validates navigable report links before they are emitted into active content.</summary>
public static class ReportLinkPolicy
{
    /// <summary>Allows HTTP(S), mail, telephone, bookmarks and ordinary relative paths; rejects executable schemes and ambiguous controls.</summary>
    public static bool IsAllowed(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Any(char.IsControl) ||
            target != target.Trim() || target.Contains('\\') || target.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }
        if (target.StartsWith('#')) return true;
        // A web-root path is relative to the current origin. On Unix, UriKind.Absolute
        // otherwise interprets it as a file URI, unlike the Windows implementation.
        if (target.StartsWith('/')) return Uri.TryCreate(target, UriKind.Relative, out _);
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return uri.Scheme is "http" or "https" or "mailto" or "tel";
        }
        return !target.Contains(':') && Uri.TryCreate(target, UriKind.Relative, out _);
    }
}
