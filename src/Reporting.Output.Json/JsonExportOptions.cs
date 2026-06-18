namespace Reporting.Output.Json;

/// <summary>Knobs for <see cref="JsonExporter"/>.</summary>
public sealed record JsonExportOptions
{
    /// <summary>If true, emits indented JSON (multi-line, 2-space). If false, compact one-line.
    /// Default true — friendly for diff snapshots and human inspection.</summary>
    public bool Indented { get; init; } = true;

    /// <summary>Unit used for x/y/width/height fields:
    /// <see cref="JsonUnit.Millimeters"/> (default — human-readable, 3 decimals),
    /// <see cref="JsonUnit.Points"/> (1/72 inch — same as PDF/SVG internals),
    /// <see cref="JsonUnit.Mils"/> (raw internal integer — exact, no precision loss).</summary>
    public JsonUnit Unit { get; init; } = JsonUnit.Millimeters;

    /// <summary>If true, emits a <c>"texts": [...]</c>-only view; lines, rects, ellipses and
    /// images are skipped. Default false — full primitive list.</summary>
    public bool TextsOnly { get; init; } = false;

    /// <summary>If true, includes <c>style</c> objects for text primitives
    /// (font family, size, color, alignment). Default true.</summary>
    public bool IncludeStyles { get; init; } = true;

    public static readonly JsonExportOptions Default = new();
}

/// <summary>Unit emitted in the JSON for x/y/width/height fields.</summary>
public enum JsonUnit
{
    /// <summary>Millimetres, rounded to 3 decimals. Default — easy to read and compare.</summary>
    Millimeters,
    /// <summary>PostScript points (1/72 inch), rounded to 3 decimals. Matches PDF/SVG units.</summary>
    Points,
    /// <summary>Raw internal mils (1/1000 inch) as integers. No precision loss.</summary>
    Mils,
}
