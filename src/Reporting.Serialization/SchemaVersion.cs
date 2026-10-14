namespace Reporting.Serialization;

/// <summary>Lightweight semver-like schema version: <c>Major.Minor</c>.</summary>
public readonly record struct SchemaVersion(int Major, int Minor) : IComparable<SchemaVersion>
{
    public static readonly SchemaVersion Current = new(1, 0);
    public static readonly SchemaVersion V1_0 = new(1, 0);

    public static SchemaVersion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('.', 2);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor))
        {
            throw new FormatException($"Invalid schema version '{text}'. Expected 'Major.Minor'.");
        }
        return new SchemaVersion(major, minor);
    }

    public int CompareTo(SchemaVersion other)
    {
        var m = Major.CompareTo(other.Major);
        return m != 0 ? m : Minor.CompareTo(other.Minor);
    }

    public static bool operator <(SchemaVersion a, SchemaVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(SchemaVersion a, SchemaVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(SchemaVersion a, SchemaVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SchemaVersion a, SchemaVersion b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Major}.{Minor}";
}
