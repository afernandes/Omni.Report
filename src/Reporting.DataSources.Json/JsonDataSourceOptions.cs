namespace Reporting.DataSources.Json;

/// <summary>Configuration for a <see cref="JsonDataSource"/>. Exactly one source location
/// (<see cref="FilePath"/>, <see cref="Url"/>, <see cref="InlineJson"/>) must be set —
/// the constructor enforces this.</summary>
/// <remarks>
/// <para>Match the RDL/My-FyiReporting <c>JsonDataReader</c> connection-string keys:
/// <c>file=</c>, <c>url=</c>, <c>table=</c>. Our <see cref="RootPath"/> takes the place of
/// the <c>table</c> selector — it's a JSON dot-path rather than a table name, which is
/// strictly more expressive (it can navigate to deeply nested arrays).</para>
/// </remarks>
public sealed class JsonDataSourceOptions
{
    /// <summary>Local file path to a JSON document. Mutually exclusive with
    /// <see cref="Url"/> and <see cref="InlineJson"/>.</summary>
    public string? FilePath { get; init; }

    /// <summary>HTTP/HTTPS URL of a JSON document. The source will issue a single
    /// <c>GET</c>; auth headers / proxies must be configured on the
    /// <see cref="HttpClient"/> passed to the <see cref="JsonDataSource"/> constructor.</summary>
    public string? Url { get; init; }

    /// <summary>Pre-loaded JSON string. Useful for tests and inline ".repx-embedded data".</summary>
    public string? InlineJson { get; init; }

    /// <summary>Dot-separated path from the document root to the array of rows. Empty /
    /// null = root IS the array. Example: <c>"data.results"</c> for
    /// <c>{ "data": { "results": [ … ] } }</c>.</summary>
    public string? RootPath { get; init; }

    /// <summary>Number of leading rows scanned for schema inference. Default 100 — large
    /// enough to catch the typical "first row is mostly nulls, real types appear later"
    /// case without blocking the paginator with a full-table scan.</summary>
    public int SchemaSampleSize { get; init; } = 100;
}
