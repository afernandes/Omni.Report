using FluentAssertions;
using Reporting.Parameters;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Read-fidelity of a <c>&lt;ReportParameter&gt;</c>'s <c>&lt;DefaultValue&gt;</c>. The importer must never lose
/// a default silently: an expression default (unrepresentable in the literal model), an unconvertible value, or
/// a locale-formatted number are all reported via <c>ImportWarnings</c> instead of being dropped (or, worse,
/// silently mis-converted as the old <c>Convert.ChangeType</c> did with comma-decimals).
/// </summary>
public class RdlParameterDefaultTests
{
    private static (ReportParameter Param, string Warnings) ImportParam(string dataType, string value)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <ReportParameters><ReportParameter Name="P">
                <DataType>{dataType}</DataType>
                <DefaultValue><Values><Value>{value}</Value></Values></DefaultValue>
              </ReportParameter></ReportParameters>
              <Body><Height>10mm</Height><ReportItems/></Body>
              <Width>100mm</Width>
              <Page><PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
                <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin></Page>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        var warnings = def.Metadata.TryGetValue("ImportWarnings", out var w) ? w : string.Empty;
        return (def.Parameters[0], warnings);
    }

    [Fact]
    public void Expression_default_is_warned_not_silently_dropped()
    {
        var (p, warnings) = ImportParam("DateTime", "=Today()");
        p.DefaultValue.Should().BeNull();
        warnings.Should().Contain("DefaultValue de expressão");
    }

    [Fact]
    public void Unconvertible_default_is_warned_not_silently_dropped()
    {
        var (p, warnings) = ImportParam("Integer", "abc");
        p.DefaultValue.Should().BeNull();
        warnings.Should().Contain("não pôde ser convertido");
    }

    [Fact]
    public void A_comma_decimal_is_rejected_not_misconverted_to_314()
    {
        // The old Convert.ChangeType(\"3,14\", decimal, Invariant) silently returned 314 (comma = thousands sep);
        // strict NumberStyles.Float rejects it, so it's dropped WITH a warning rather than corrupting the value.
        var (p, warnings) = ImportParam("Decimal", "3,14");
        p.DefaultValue.Should().NotBe(314m);
        p.DefaultValue.Should().BeNull();
        warnings.Should().Contain("não pôde ser convertido");
    }

    [Fact]
    public void Valid_invariant_defaults_still_parse()
    {
        ImportParam("Integer", "2024").Param.DefaultValue.Should().Be(2024);
        ImportParam("Decimal", "3.14").Param.DefaultValue.Should().Be(3.14m);
        ImportParam("Float", "2.5").Param.DefaultValue.Should().Be(2.5d);
        ImportParam("Boolean", "true").Param.DefaultValue.Should().Be(true);
        ImportParam("String", "Olá").Param.DefaultValue.Should().Be("Olá");
    }
}
