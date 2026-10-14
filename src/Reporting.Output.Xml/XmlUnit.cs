namespace Reporting.Output.Xml;

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
