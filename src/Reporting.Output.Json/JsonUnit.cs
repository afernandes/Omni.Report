namespace Reporting.Output.Json;

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
