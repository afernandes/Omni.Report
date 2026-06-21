using FluentAssertions;
using Reporting.Bands;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// End-to-end designer round-trip tests for every RDL Phase 1 feature added to the
/// ViewModels: ElementAction (Hyperlink / BookmarkLink / Drillthrough), Bookmark +
/// DocumentMapLabel, ToggleItem + InitiallyHidden, band-level PageBreak, Detail-level
/// NoRowsMessage / Filter / Sort, Group-level Filter / Sort / Variables, DataSource-level
/// CalculatedFields / Filter / Sort. Each test sets a field via the VM, saves to .repx,
/// reloads, and asserts the value survived the byte trip — proves the Designer surfaces
/// genuine RDL state, not stale local-only chrome.
/// </summary>
public class RdlDesignerRoundTripTests
{
    [Fact]
    public void Imported_rdl_query_is_live_in_the_designer()
    {
        // An imported .rdl DataSet query must land on the designer's live convention (_sql / _storedProc /
        // param:@x) so it opens in the data-source editor and executes — previously it went to dead keys.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <DataSets><DataSet Name="Vendas"><Query>
                <CommandText>EXEC sp_Vendas</CommandText>
                <CommandType>StoredProcedure</CommandType>
                <QueryParameters>
                  <QueryParameter Name="@Ano"><Value>=Parameters!Ano.Value</Value></QueryParameter>
                  <QueryParameter Name="@Fixo"><Value>42</Value></QueryParameter>
                </QueryParameters>
              </Query></DataSet></DataSets>
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        var def = new Reporting.Serialization.RdlImporter().ImportXml(rdl);
        var ds = DesignerDataSource.FromDefinition(def.DataSources.Single());

        ds.Sql.Should().Be("EXEC sp_Vendas");
        ds.IsStoredProcedure.Should().BeTrue();
        ds.SqlParameters.Should().Contain(p => p.SqlName == "@Ano" && p.ReportParameter == "Ano" && p.Literal == null);
        ds.SqlParameters.Should().Contain(p => p.SqlName == "@Fixo" && p.ReportParameter == null && p.Literal == "42");
    }

    [Fact]
    public void Hyperlink_action_round_trips_through_designer_save_load()
    {
        var state = NewState();
        var el = AddTextBox(state, e =>
        {
            e.HasAction = true;
            e.ActionKind = ActionKind.Hyperlink;
            e.Hyperlink = "https://example.com/{Fields.Id}";
        });
        var reloaded = SaveLoad(state);
        var loaded = FirstDetailElement(reloaded);
        loaded.HasAction.Should().BeTrue();
        loaded.ActionKind.Should().Be(ActionKind.Hyperlink);
        loaded.Hyperlink.Should().Be("https://example.com/{Fields.Id}");
    }

    [Fact]
    public void Drillthrough_action_with_parameters_round_trips_through_designer()
    {
        var state = NewState();
        AddTextBox(state, e =>
        {
            e.HasAction = true;
            e.ActionKind = ActionKind.DrillthroughReport;
            e.DrillthroughReportName = "Detalhes";
            e.DrillthroughParameters.Add(new DrillthroughParameterRule { Name = "PedidoId", Value = "{Fields.Id}" });
            e.DrillthroughParameters.Add(new DrillthroughParameterRule { Name = "Ano", Value = "{Parameters.Ano}", Omit = true });
        });
        var reloaded = SaveLoad(state);
        var loaded = FirstDetailElement(reloaded);
        loaded.ActionKind.Should().Be(ActionKind.DrillthroughReport);
        loaded.DrillthroughReportName.Should().Be("Detalhes");
        loaded.DrillthroughParameters.Should().HaveCount(2);
        loaded.DrillthroughParameters[1].Omit.Should().BeTrue();
    }

    [Fact]
    public void Bookmark_documentmap_toggle_round_trip_through_designer()
    {
        var state = NewState();
        AddTextBox(state, e =>
        {
            e.Bookmark = "anchor-total";
            e.DocumentMapLabel = "Total geral";
            e.ToggleItemId = "expander";
            e.InitiallyHidden = true;
        });
        var reloaded = SaveLoad(state);
        var loaded = FirstDetailElement(reloaded);
        loaded.Bookmark.Should().Be("anchor-total");
        loaded.DocumentMapLabel.Should().Be("Total geral");
        loaded.ToggleItemId.Should().Be("expander");
        loaded.InitiallyHidden.Should().BeTrue();
    }

    [Theory]
    [InlineData(PageBreak.Start)]
    [InlineData(PageBreak.End)]
    [InlineData(PageBreak.StartAndEnd)]
    public void Detail_band_page_break_round_trips_through_designer(PageBreak rule)
    {
        var state = NewState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.PageBreak = rule;
        var reloaded = SaveLoad(state);
        reloaded.Report.FindBand(DesignerBandKind.Detail)!.PageBreak.Should().Be(rule);
    }

    [Fact]
    public void Detail_no_rows_message_and_filter_sort_round_trip_through_designer()
    {
        var state = NewState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.NoRowsMessage = "Sem dados.";
        detail.FilterExpression = "Fields.Total > 0";
        detail.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields.Cliente" });
        detail.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields.Total", Direction = Reporting.Data.SortDirection.Descending });

        var reloaded = SaveLoad(state);
        var d = reloaded.Report.FindBand(DesignerBandKind.Detail)!;
        d.NoRowsMessage.Should().Be("Sem dados.");
        d.FilterExpression.Should().Be("Fields.Total > 0");
        d.SortExpressions.Should().HaveCount(2);
        d.SortExpressions[1].Direction.Should().Be(Reporting.Data.SortDirection.Descending);
    }

    [Fact]
    public void Group_filter_sort_variables_and_page_break_round_trip_through_designer()
    {
        var state = NewState();
        // Add a GroupHeader band (mirrors what the user does via "Adicionar grupo").
        var gh = new BandViewModel(DesignerBandKind.GroupHeader, Unit.FromMm(8))
        {
            GroupName = "PorCliente",
            GroupExpression = "Fields.Cliente",
            PageBreak = PageBreak.Between,
            FilterExpression = "Sum(Fields.Total) > 100",
        };
        gh.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields.Cliente" });
        gh.Variables.Add(new GroupVariableRule { Name = "Subtotal", Expression = "Sum(Fields.Total)" });
        // Insert before Detail so the FromDefinition reload places it in the correct slot.
        var detailIdx = state.Report.Bands.IndexOf(state.Report.FindBand(DesignerBandKind.Detail)!);
        state.Report.Bands.Insert(detailIdx, gh);

        var reloaded = SaveLoad(state);
        var rgh = reloaded.Report.Bands.FirstOrDefault(b => b.Kind == DesignerBandKind.GroupHeader);
        rgh.Should().NotBeNull();
        rgh!.GroupName.Should().Be("PorCliente");
        rgh.PageBreak.Should().Be(PageBreak.Between);
        rgh.FilterExpression.Should().Be("Sum(Fields.Total) > 100");
        rgh.SortExpressions.Should().ContainSingle();
        rgh.Variables.Should().ContainSingle();
        rgh.Variables[0].Name.Should().Be("Subtotal");
    }

    [Fact]
    public void Data_source_calculated_fields_filter_sort_round_trip_through_designer()
    {
        var state = NewState();
        var ds = new DesignerDataSource("Pedidos", new[] { new DesignerField("Total", DesignerFieldType.Number) });
        ds.CalculatedFields.Add(new DesignerCalculatedField("Imposto", "Fields.Total * 0.18", DesignerFieldType.Money));
        ds.CalculatedFields.Add(new DesignerCalculatedField("TotalComImposto", "Fields.Total + Fields.Imposto"));
        ds.FilterExpression = "Fields.Status == 'Pago'";
        ds.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields.Data", Direction = Reporting.Data.SortDirection.Descending });
        state.DataSources.Add(ds);

        var reloaded = SaveLoad(state);
        var rds = reloaded.DataSources.FirstOrDefault(d => d.Name == "Pedidos");
        rds.Should().NotBeNull();
        rds!.CalculatedFields.Should().HaveCount(2);
        rds.CalculatedFields[0].Name.Should().Be("Imposto");
        rds.CalculatedFields[0].ResultType.Should().Be(DesignerFieldType.Money);
        rds.FilterExpression.Should().Be("Fields.Status == 'Pago'");
        rds.SortExpressions.Should().ContainSingle();
        rds.SortExpressions[0].Direction.Should().Be(Reporting.Data.SortDirection.Descending);
    }

    [Fact]
    public void Clone_preserves_all_rdl_extensions()
    {
        // Copy / paste uses Element.Clone(). Make sure RDL fields tag along — without this,
        // the clipboard would lose the action / bookmark / drill-down state silently.
        var src = new ElementViewModel(DesignerElementKind.TextBox, "src")
        {
            Bookmark = "src-bm",
            DocumentMapLabel = "Src label",
            ToggleItemId = "src-toggle",
            InitiallyHidden = true,
            HasAction = true,
            ActionKind = ActionKind.Hyperlink,
            Hyperlink = "https://x",
        };
        src.DrillthroughParameters.Add(new DrillthroughParameterRule { Name = "Id", Value = "1" });
        var clone = src.Clone();
        clone.Bookmark.Should().Be("src-bm");
        clone.DocumentMapLabel.Should().Be("Src label");
        clone.ToggleItemId.Should().Be("src-toggle");
        clone.InitiallyHidden.Should().BeTrue();
        clone.HasAction.Should().BeTrue();
        clone.Hyperlink.Should().Be("https://x");
        clone.DrillthroughParameters.Should().ContainSingle();
        // Clones should NOT share the parameter list — mutating the source's list
        // must not affect the clone (verified by reference-inequality).
        clone.DrillthroughParameters.Should().NotBeSameAs(src.DrillthroughParameters);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static DesignerState NewState() => new();

    private static ElementViewModel AddTextBox(DesignerState state, Action<ElementViewModel> configure)
    {
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var el = new ElementViewModel(DesignerElementKind.TextBox, Guid.NewGuid().ToString("n"))
        {
            Expression = "{Fields.X}",
            X = Unit.FromMm(0), Y = Unit.FromMm(0),
            Width = Unit.FromMm(50), Height = Unit.FromMm(6),
        };
        configure(el);
        detail.AddElement(el);
        return el;
    }

    private static ElementViewModel FirstDetailElement(DesignerState s)
        => s.Report.FindBand(DesignerBandKind.Detail)!.Elements[0];

    private static DesignerState SaveLoad(DesignerState s)
    {
        var bytes = s.Save();
        var loaded = new DesignerState();
        loaded.Load(bytes);
        return loaded;
    }
}
