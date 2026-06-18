using FluentAssertions;
using Reporting.DataSources.FileSystem;
using Xunit;

namespace Reporting.DataSources.Providers.Tests;

/// <summary>
/// Tests for <see cref="FileSystemDataSource"/>. Each test creates a temp directory
/// with a known layout, runs the source against it, and inspects the row set.
/// Cleanup is best-effort in a finally block so a failed assertion doesn't leak
/// directories on the CI runner.
/// </summary>
public class FileSystemDataSourceTests : IDisposable
{
    private readonly string _root;

    public FileSystemDataSourceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "OmniReportFS-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task Schema_is_fixed_and_exposed_upfront()
    {
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions { RootDirectory = _root });
        ds.Schema.Fields.Should().HaveCount(10);
        ds.Schema.Fields.Select(f => f.Name).Should().BeEquivalentTo(new[]
        {
            "Id", "ParentId", "Name", "FullName", "Extension",
            "IsDirectory", "Size", "CreationTime", "LastAccessTime", "LastWriteTime",
        });
    }

    [Fact]
    public async Task Flat_directory_yields_one_row_per_file()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "a.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(_root, "b.txt"), "22");
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions
        {
            RootDirectory = _root,
            Recursive = false,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => (string)r["Name"]!).Should().BeEquivalentTo(new[] { "a.txt", "b.txt" });
        rows.All(r => (bool)r["IsDirectory"]! == false).Should().BeTrue();
    }

    [Fact]
    public async Task File_pattern_filters_matched_files()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "a.log"), "1");
        await File.WriteAllTextAsync(Path.Combine(_root, "b.txt"), "2");
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions
        {
            RootDirectory = _root,
            FilePattern = "*.log",
            Recursive = false,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().ContainSingle();
        rows[0]["Name"].Should().Be("a.log");
    }

    [Fact]
    public async Task Recursive_enumeration_includes_subdirectory_files()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "root.txt"), "x");
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "child.txt"), "y");

        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions
        {
            RootDirectory = _root,
            Recursive = true,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Select(r => (string)r["Name"]!).Should().Contain(new[] { "root.txt", "sub", "child.txt" });
    }

    [Fact]
    public async Task Parent_id_links_files_to_their_directory()
    {
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "x.txt"), "y");
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions { RootDirectory = _root });
        var rows = await ds.ReadAsync().ToListAsync();
        // The "sub" directory row should have an Id; the "x.txt" file row's ParentId
        // should point at it.
        var dirRow = rows.First(r => (string)r["Name"]! == "sub");
        var fileRow = rows.First(r => (string)r["Name"]! == "x.txt");
        fileRow["ParentId"].Should().Be(dirRow["Id"]);
    }

    [Fact]
    public async Task Trim_empty_drops_subdirs_with_no_matching_files()
    {
        // Layout:
        //   _root/
        //     keep/
        //       a.log
        //     empty/
        //       b.txt   ← not matched by *.log
        Directory.CreateDirectory(Path.Combine(_root, "keep"));
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        await File.WriteAllTextAsync(Path.Combine(_root, "keep", "a.log"), "x");
        await File.WriteAllTextAsync(Path.Combine(_root, "empty", "b.txt"), "y");
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions
        {
            RootDirectory = _root,
            FilePattern = "*.log",
            TrimEmptyDirectories = true,
        });
        var names = (await ds.ReadAsync().ToListAsync()).Select(r => (string)r["Name"]!).ToList();
        names.Should().Contain("keep");
        names.Should().Contain("a.log");
        names.Should().NotContain("empty");
    }

    [Fact]
    public async Task IncludeDirectories_false_emits_only_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_root, "sub", "x.txt"), "y");
        var ds = new FileSystemDataSource("Test", new FileSystemDataSourceOptions
        {
            RootDirectory = _root,
            IncludeDirectories = false,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.All(r => (bool)r["IsDirectory"]! == false).Should().BeTrue();
        rows.Should().ContainSingle(r => (string)r["Name"]! == "x.txt");
    }

    [Fact]
    public void Missing_root_directory_throws()
    {
        var act = () => new FileSystemDataSource("Test",
            new FileSystemDataSourceOptions { RootDirectory = "/no/such/path-" + Guid.NewGuid() });
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
