using System.Runtime.CompilerServices;

namespace Reporting.DataSources.FileSystem;

/// <summary>
/// Enumerates the filesystem under a root directory and exposes each entry (file or
/// folder) as a row with the standard metadata columns.
/// </summary>
/// <remarks>
/// <para><b>Schema</b> — fixed: Id (int), ParentId (int), Name (string), FullName (string),
/// Extension (string), IsDirectory (bool), Size (long), CreationTime (DateTime),
/// LastAccessTime (DateTime), LastWriteTime (DateTime). The schema is exposed up-front
/// (not lazily inferred) because it's stable; designers can show the field tree before
/// any rows have been read.</para>
///
/// <para><b>Id / ParentId</b> — each row gets a row-index Id; ParentId points at the
/// containing-directory row (0 for the root itself, which is not emitted). This lets
/// reports build a tree view by self-joining the source: <c>FilterExpression =
/// "Fields.ParentId == 5"</c> shows the children of node 5.</para>
///
/// <para><b>Trim empty</b> — when
/// <see cref="FileSystemDataSourceOptions.TrimEmptyDirectories"/> is true we make a
/// second pass after enumeration to drop directory rows whose descendant set contains
/// no files (matches the RDL behaviour where you DON'T want empty subfolders in a log
/// report).</para>
/// </remarks>
public sealed class FileSystemDataSource : IReportDataSource
{
    private readonly FileSystemDataSourceOptions _opts;

    public static readonly IReportRecordSchema FixedSchema = new ReportRecordSchema(new[]
    {
        new ReportField("Id",             typeof(int)),
        new ReportField("ParentId",       typeof(int)),
        new ReportField("Name",           typeof(string)),
        new ReportField("FullName",       typeof(string)),
        new ReportField("Extension",      typeof(string)),
        new ReportField("IsDirectory",    typeof(bool)),
        new ReportField("Size",           typeof(long)),
        new ReportField("CreationTime",   typeof(DateTime)),
        new ReportField("LastAccessTime", typeof(DateTime)),
        new ReportField("LastWriteTime",  typeof(DateTime)),
    });

    public FileSystemDataSource(string name, FileSystemDataSourceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.RootDirectory);
        if (!Directory.Exists(options.RootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"FileSystemDataSource '{name}': root directory does not exist: {options.RootDirectory}");
        }
        Name = name;
        _opts = options;
    }

    public string Name { get; }
    public IReportRecordSchema Schema => FixedSchema;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var rowKinds = new List<bool>(); // true = directory
        int nextId = 1;
        // Walk depth-first so children appear after their parent — keeps the tree
        // intuition stable for reports rendering a hierarchical listing.
        WalkDirectory(new DirectoryInfo(_opts.RootDirectory), parentId: 0, ref nextId,
            rows, rowKinds, cancellationToken);

        if (_opts.TrimEmptyDirectories)
        {
            rows = TrimEmpty(rows, rowKinds);
        }

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new DictionaryRecord(FixedSchema, row);
            await Task.Yield();
        }
    }

    // ── Recursive traversal ─────────────────────────────────────────────────────

    private void WalkDirectory(
        DirectoryInfo dir,
        int parentId,
        ref int nextId,
        List<IReadOnlyDictionary<string, object?>> rows,
        List<bool> rowKinds,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Files in this directory.
        FileInfo[] files;
        try { files = dir.GetFiles(_opts.FilePattern); }
        catch (UnauthorizedAccessException) { files = Array.Empty<FileInfo>(); }
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            rows.Add(FileRow(f, nextId++, parentId));
            rowKinds.Add(false);
        }

        // Sub-directories (always traverse when recursive — even if filter excludes them
        // from being emitted as their own row).
        if (!_opts.Recursive) return;
        DirectoryInfo[] subs;
        try { subs = dir.GetDirectories(_opts.DirectoryPattern); }
        catch (UnauthorizedAccessException) { subs = Array.Empty<DirectoryInfo>(); }
        foreach (var sub in subs)
        {
            ct.ThrowIfCancellationRequested();
            int dirId = nextId++;
            if (_opts.IncludeDirectories)
            {
                rows.Add(DirRow(sub, dirId, parentId));
                rowKinds.Add(true);
            }
            else
            {
                // Don't emit the directory row but still use a synthetic id so children
                // can point at SOMETHING consistent — they reference the parent's parent.
                dirId = parentId;
                nextId--; // give back the id; nothing consumed it
            }
            WalkDirectory(sub, dirId, ref nextId, rows, rowKinds, ct);
        }
    }

    // ── Row construction ────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> FileRow(FileInfo f, int id, int parentId)
    {
        // We intentionally tolerate missing files (timing window between enumeration and
        // attribute read) by reading inside a try — a missing entry shows up with zero
        // timestamps rather than crashing the whole iteration.
        long size = 0;
        DateTime ctime = DateTime.MinValue, atime = DateTime.MinValue, wtime = DateTime.MinValue;
        try { size = f.Length; } catch { /* swallow */ }
        try { ctime = f.CreationTime; } catch { /* swallow */ }
        try { atime = f.LastAccessTime; } catch { /* swallow */ }
        try { wtime = f.LastWriteTime; } catch { /* swallow */ }
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = id,
            ["ParentId"] = parentId,
            ["Name"] = f.Name,
            ["FullName"] = f.FullName,
            ["Extension"] = f.Extension,
            ["IsDirectory"] = false,
            ["Size"] = size,
            ["CreationTime"] = ctime,
            ["LastAccessTime"] = atime,
            ["LastWriteTime"] = wtime,
        };
    }

    private static IReadOnlyDictionary<string, object?> DirRow(DirectoryInfo d, int id, int parentId)
    {
        DateTime ctime = DateTime.MinValue, atime = DateTime.MinValue, wtime = DateTime.MinValue;
        try { ctime = d.CreationTime; } catch { /* swallow */ }
        try { atime = d.LastAccessTime; } catch { /* swallow */ }
        try { wtime = d.LastWriteTime; } catch { /* swallow */ }
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = id,
            ["ParentId"] = parentId,
            ["Name"] = d.Name,
            ["FullName"] = d.FullName,
            ["Extension"] = string.Empty,
            ["IsDirectory"] = true,
            ["Size"] = 0L,
            ["CreationTime"] = ctime,
            ["LastAccessTime"] = atime,
            ["LastWriteTime"] = wtime,
        };
    }

    // ── Empty-directory trimming ────────────────────────────────────────────────

    /// <summary>Drops directory rows whose descendant set contains no files. We walk
    /// the rows once to count file descendants per directory id; then filter directory
    /// rows whose count is zero. File rows are always kept.</summary>
    private static List<IReadOnlyDictionary<string, object?>> TrimEmpty(
        List<IReadOnlyDictionary<string, object?>> rows,
        List<bool> rowKinds)
    {
        // Build a parent → file-count map by walking the rows.
        // First-pass: tally direct file children per parent id.
        var fileCountByParent = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if ((bool)row["IsDirectory"]!) continue;
            var pid = (int)row["ParentId"]!;
            fileCountByParent[pid] = fileCountByParent.GetValueOrDefault(pid) + 1;
        }
        // Second-pass: roll the counts UP through the parent chain so a directory whose
        // grandchildren contain files isn't trimmed even if its direct children are
        // all empty sub-directories.
        var parentByDir = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if ((bool)row["IsDirectory"]!)
            {
                parentByDir[(int)row["Id"]!] = (int)row["ParentId"]!;
            }
        }
        var rolledUp = new Dictionary<int, int>(fileCountByParent);
        foreach (var (dirId, parentId) in parentByDir)
        {
            // Walk upward, accumulating this dir's file count into every ancestor.
            var count = rolledUp.GetValueOrDefault(dirId);
            if (count == 0) continue;
            var p = parentId;
            while (p != 0 && parentByDir.ContainsKey(p))
            {
                rolledUp[p] = rolledUp.GetValueOrDefault(p) + count;
                p = parentByDir[p];
            }
        }
        // Final-pass: keep file rows always; keep dir rows only when their rolled-up
        // file count is > 0.
        var filtered = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        foreach (var row in rows)
        {
            if (!(bool)row["IsDirectory"]!)
            {
                filtered.Add(row);
                continue;
            }
            var id = (int)row["Id"]!;
            if (rolledUp.GetValueOrDefault(id) > 0) filtered.Add(row);
        }
        return filtered;
    }
}
