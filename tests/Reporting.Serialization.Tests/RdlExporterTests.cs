using System.Text;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Serialization;
using Reporting.Serialization.Internal;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// The RDL exporter (saving <c>.rdl</c>) — the inverse of the importer, enabling
/// <c>.rdl → import → edit → export → .rdl</c>. PR1 covers the reverse expression translation and the page
/// skeleton; report items, datasets and Tablix arrive in later phases.
/// </summary>
public class RdlExporterTests
{
    // ── Reverse expression translation (OmniReport → RDL VB/SSRS) ─────────────────

    [Theory]
    [InlineData("Fields.Total")]
    [InlineData("Parameters.Ano")]
    [InlineData("ReportItems.Titulo")]
    [InlineData("PageNumber")]
    [InlineData("TotalPages")]
    [InlineData("Now")]
    [InlineData("UserName")]
    [InlineData("Concat(Fields.Nome, Fields.Sobrenome)")]
    [InlineData("Concat(\"Olá \", Fields.Nome)")]
    [InlineData("IIf(Fields.Total < 0, \"Vermelho\", \"Preto\")")]
    [InlineData("Like(Fields.Nome, \"A*\")")]
    public void Expression_reverse_then_forward_is_identity(string omni)
    {
        // ToRdl must be the inverse of RdlExpression.Convert for the common forms — the property that makes
        // .rdl round-trip. (The leading '=' marks an RDL expression.)
        var rdl = RdlExpressionReverse.ToRdl(omni);
        rdl.Should().StartWith("=");
        RdlExpression.Convert(rdl).Should().Be(omni);
    }

    [Fact]
    public void Reverse_maps_refs_and_globals_to_vb_syntax()
    {
        RdlExpressionReverse.ToRdl("Fields.X").Should().Be("=Fields!X.Value");
        RdlExpressionReverse.ToRdl("Parameters.P").Should().Be("=Parameters!P.Value");
        RdlExpressionReverse.ToRdl("ReportItems.T").Should().Be("=ReportItems!T.Value");
        RdlExpressionReverse.ToRdl("PageNumber").Should().Be("=Globals!PageNumber");
        RdlExpressionReverse.ToRdl("TotalPages").Should().Be("=Globals!TotalPages");
        RdlExpressionReverse.ToRdl("Now").Should().Be("=Globals!ExecutionTime");
        RdlExpressionReverse.ToRdl("UserName").Should().Be("=User!UserID");
    }

    [Fact]
    public void Reverse_unwraps_concat_to_ampersand_and_like_to_infix()
    {
        RdlExpressionReverse.ToRdl("Concat(Fields.a, Fields.b)")
            .Should().Be("=Fields!a.Value & Fields!b.Value");
        RdlExpressionReverse.ToRdl("Like(Fields.nome, \"A*\")")
            .Should().Be("=Fields!nome.Value Like \"A*\"");
    }

    [Fact]
    public void Reverse_does_not_touch_a_field_named_like_a_global()
    {
        // Fields.PageNumber must become Fields!PageNumber.Value, NOT Globals!PageNumber.
        RdlExpressionReverse.ToRdl("Fields.PageNumber").Should().Be("=Fields!PageNumber.Value");
    }

    [Fact]
    public void Empty_expression_reverses_to_empty()
        => RdlExpressionReverse.ToRdl("").Should().BeEmpty();

    // ── Exporter skeleton (page setup) ────────────────────────────────────────────

    [Fact]
    public void Save_emits_a_Report_in_the_official_SSRS_namespace_with_page_setup()
    {
        var def = new ReportDefinition("Vendas", PageSetup.A4Portrait, DetailBand.Empty);
        var xml = Encoding.UTF8.GetString(new RdlExporter().SaveToBytes(def));

        xml.Should().Contain("schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition");
        xml.Should().Contain("<Report").And.Contain("<Body>").And.Contain("<Page>");
        // A4 = 8268 mils → 210.0072mm (Unit.Mils is integer); the mm string round-trips losslessly via
        // ParseSize. Assert structure here; the exact value is checked by Page_setup_round_trips_through_rdl.
        xml.Should().Contain("<PageWidth>210").And.Contain("<PageHeight>297").And.Contain("mm</PageWidth>");
    }

    [Fact]
    public void Page_setup_round_trips_through_rdl()
    {
        var def = new ReportDefinition("R",
            PageSetup.A4Portrait with
            {
                Margins = Thickness.Uniform(Unit.FromMm(15)),
                Columns = 2,
                ColumnSpacing = Unit.FromMm(6),
            },
            DetailBand.Empty);

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        back.PageSetup.PageWidth.ToMm().Should().BeApproximately(210, 0.1);
        back.PageSetup.PageHeight.ToMm().Should().BeApproximately(297, 0.1);
        back.PageSetup.Margins.Left.ToMm().Should().BeApproximately(15, 0.1);
        back.PageSetup.Columns.Should().Be(2);
        back.PageSetup.ColumnSpacing.ToMm().Should().BeApproximately(6, 0.1);
    }

    [Fact]
    public void Report_language_round_trips()
    {
        var def = new ReportDefinition("R", PageSetup.A4Portrait, DetailBand.Empty)
        {
            Metadata = new Reporting.Common.EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["Language"] = "pt-BR" }),
        };
        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));
        back.Metadata.Should().ContainKey("Language").WhoseValue.Should().Be("pt-BR");
    }
}
