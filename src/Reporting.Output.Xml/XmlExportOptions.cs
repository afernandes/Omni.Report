namespace Reporting.Output.Xml;

/// <summary>Knobs for <see cref="XmlExporter"/> (mirrors the JSON exporter's options).</summary>
public sealed record XmlExportOptions
{
    /// <summary>If true, emits indented XML (multi-line, 2-space). Default true — diff-friendly.</summary>
    public bool Indented { get; init; } = true;

    /// <summary>Unit used for x/y/width/height: <see cref="XmlUnit.Millimeters"/> (default, 3 decimals),
    /// <see cref="XmlUnit.Points"/> (1/72 inch), or <see cref="XmlUnit.Mils"/> (raw internal integer).</summary>
    public XmlUnit Unit { get; init; } = XmlUnit.Millimeters;

    /// <summary>If true, emits a texts-only view; lines/rects/ellipses/images are skipped. Default false.</summary>
    public bool TextsOnly { get; init; } = false;

    /// <summary>If true, includes <c>&lt;style&gt;</c> for text primitives (font/size/color/alignment). Default true.</summary>
    public bool IncludeStyles { get; init; } = true;

    public static readonly XmlExportOptions Default = new();
}

/// <summary>Unit emitted in the XML for x/y/width/height fields.</summary>
public enum XmlUnit
{
    /// <summary>Millimetres, rounded to 3 decimals. Default.</summary>
    Millimeters,
    /// <summary>PostScript points (1/72 inch), rounded to 3 decimals.</summary>
    Points,
    /// <summary>Raw internal mils (1/1000 inch) as integers. No precision loss.</summary>
    Mils,
}
