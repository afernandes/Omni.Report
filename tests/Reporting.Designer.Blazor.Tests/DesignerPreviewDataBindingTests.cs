using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Reproduces the designer host's "load a sample → preview" path (DesignerPage.LoadSample):
/// a built <see cref="Report"/> is converted to a view-model and rebuilt into a definition, then
/// paginated against the sample's own data registry. Regression guard for the bug where the
/// preview rendered structure but no data (empty detail cells) after the VM round-trip.
/// </summary>
public class DesignerPreviewDataBindingTests
{
    [Fact]
    public async Task Designer_vm_round_trip_still_binds_detail_data_in_the_preview()
    {
        var data = new[]
        {
            new { Produto = "Caneta", Preco = 3.20m },
            new { Produto = "Caderno", Preco = 34.90m },
        };
        var report = ReportBuilder.Create("Mini")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Itens", data)
            .Detail(d => d.Height(8)
                .Text("Fields.Produto").Bounds(0, 0, 60, 6)
                .Text("{Fields.Preco:C}").Bounds(60, 0, 40, 6))
            .Build();

        // Host flow (DesignerPage.LoadSample): Definition -> VM -> active report, AND sync the
        // designer's data-source catalog from the sample's definition so BuildDefinition's primary
        // source matches the preview data registry. Without this sync the detail band binds to the
        // default catalog source ("Vendas") and iterates zero rows → empty preview.
        var vm = ReportDefinitionViewModel.FromDefinition(report.Definition);
        var state = new DesignerState();
        state.ReplaceActiveReport(vm);
        state.DataSources.Clear();
        foreach (var ds in report.Definition.DataSources)
        {
            state.DataSources.Add(DesignerDataSource.FromDefinition(ds));
        }
        var definition2 = state.BuildDefinition();

        // Preview paginates the rebuilt definition with the sample's data (PreviewDataRegistry).
        var rendered = await new ReportPaginator().PaginateAsync(new PaginationRequest
        {
            Definition = definition2,
            DataSources = report.DataSources,
        });
        var texts = rendered.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        definition2.DataSources.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Itens",
            "the rebuilt definition's primary source must match the loaded report, not the default catalog");
        texts.Should().Contain("Caneta", "the detail TextBox must still bind data after the designer load");
        texts.Should().Contain("Caderno");
        texts.Should().Contain(t => t.Contains("34,90"), "the formatted currency expression must bind too");
    }
}
