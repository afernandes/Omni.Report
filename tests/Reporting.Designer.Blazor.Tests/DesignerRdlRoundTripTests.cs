using System.Text;
using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The Designer's RDL menu options: <see cref="DesignerState.SaveRdl"/> (Exportar RDL) and
/// <see cref="DesignerState.LoadRdl"/> (Importar RDL) project the native report to/from the RDL 2016 format
/// through the same restore path as <c>.repx</c> — a report authored in the Designer survives the round-trip.
/// </summary>
public class DesignerRdlRoundTripTests
{
    [Fact]
    public void Exported_rdl_is_a_2016_report_definition()
    {
        var rdl = new DesignerState().SaveRdl();
        var xml = Encoding.UTF8.GetString(rdl);
        xml.Should().Contain("<Report");
        xml.Should().Contain("schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition");
    }

    [Fact]
    public void A_parameter_round_trips_through_designer_rdl_export_and_import()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("ano", DesignerFieldType.Number, "2026")
        {
            Prompt = "Ano de referência",
        });

        // Exportar RDL → Importar RDL into a fresh designer.
        var rdl = state.SaveRdl();
        var reloaded = new DesignerState();
        reloaded.LoadRdl(rdl);

        reloaded.Parameters.Should().ContainSingle("the report parameter survives the RDL round-trip");
        var p = reloaded.Parameters[0];
        p.Name.Should().Be("ano");
        p.Prompt.Should().Be("Ano de referência");
        p.Type.Should().Be(DesignerFieldType.Number); // double ↔ RDL Float ↔ Number
    }
}
