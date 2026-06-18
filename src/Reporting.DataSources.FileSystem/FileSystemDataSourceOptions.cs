namespace Reporting.DataSources.FileSystem;

/// <summary>Configuration for a <see cref="FileSystemDataSource"/>. Required field:
/// <see cref="RootDirectory"/>. Everything else has sensible defaults.</summary>
public sealed class FileSystemDataSourceOptions
{
    /// <summary>Absolute or relative path to the directory whose contents should be
    /// enumerated. The directory itself is NOT included in the output — only its
    /// children (recursively when <see cref="Recursive"/> is true).</summary>
    public required string RootDirectory { get; init; }

    /// <summary>Glob pattern matched against file names. Default <c>*</c> (every file).
    /// Example: <c>*.log</c>, <c>*.{json,xml}</c>.</summary>
    public string FilePattern { get; init; } = "*";

    /// <summary>Glob pattern matched against directory names. Default <c>*</c> (every
    /// directory). Use this to limit recursion to specific sub-trees.</summary>
    public string DirectoryPattern { get; init; } = "*";

    /// <summary>When true, recurses into sub-directories. Default true (matches the
    /// RDL/My-FyiReporting behaviour). Set to false for a single-level listing.</summary>
    public bool Recursive { get; init; } = true;

    /// <summary>When true, rows representing directories with no matching files below
    /// are pruned from the output. Useful when the report only displays files (the
    /// empty parent dirs would just be clutter). Default false.</summary>
    public bool TrimEmptyDirectories { get; init; }

    /// <summary>Whether each directory entry itself becomes a row. Default true.
    /// Set to false when the report only cares about files (the parent-id hierarchy
    /// is then broken, but it's the simplest case).</summary>
    public bool IncludeDirectories { get; init; } = true;
}
