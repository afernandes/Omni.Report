using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed record StyledRow(string Nome, string Cor, double Tamanho);

/// <summary>
/// Covers SSRS-style per-property expression bindings: a property bound via
/// <see cref="ReportElement.PropertyExpressions"/> is evaluated per row at render time and overrides
/// the static value. The static value remains the graceful fallback when a binding can't be evaluated.
/// Bindings are applied in <c>BandRenderer</c> before everything else.
/// </summary>
public class PropertyExpressionRenderTests
{
    private static async Task<DrawTextPrimitive> RenderTextBox(StyledRow row, params (string Path, string Expr)[] bindings)
    {
        var pe = bindings.Length == 0
            ? EquatableDictionary<string, string>.Empty
            : new EquatableDictionary<string, string>(bindings.ToDictionary(b => b.Path, b => b.Expr));

        var tb = new TextBoxElement
        {
            Id = "t",
            Expression = "Fields.Nome",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
            Style = Style.Default with { ForeColor = Color.Black, Font = new Font("Arial", 10) },
            PropertyExpressions = pe,
        };
        var detail = new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { tb }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [row]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });

        return report.Pages[0].Primitives.OfType<DrawTextPrimitive>().First();
    }

    [Fact]
    public async Task Binds_fore_colour_from_an_expression_overriding_the_static_value()
    {
        var text = await RenderTextBox(new StyledRow("A", "#CC0000", 10), ("Style.ForeColor", "Fields.Cor"));
        text.Style.ForeColor.Should().Be(Color.FromHex("#CC0000"), "the expression result coerces to Color and overrides the static black");
    }

    [Fact]
    public async Task Binds_font_size_from_an_expression_on_a_nested_record_path()
    {
        var text = await RenderTextBox(new StyledRow("A", "#000000", 18), ("Style.Font.Size", "Fields.Tamanho"));
        text.Style.Font.Size.Should().Be(18, "a nested path (Style.Font.Size) rebuilds the record chain immutably");
    }

    [Fact]
    public async Task Binds_an_enum_alignment_from_an_expression_coercing_by_name()
    {
        // Enums are now expression-bindable by default (SSRS-style "bind any property"). A string result
        // coerces to the enum by name, case-insensitively — proving the newly-enabled category end-to-end.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Style.HorizontalAlignment", "'Center'"));
        text.Style.HorizontalAlignment.Should().Be(HorizontalAlignment.Center, "the expression result coerces to the enum by name, overriding the static default");
    }

    [Fact]
    public async Task Binds_bounds_width_through_a_struct_segment()
    {
        // Bounds is a record STRUCT (Rectangle) with no <Clone>$ — the binder must reconstruct it via its
        // positional ctor. Before the fix this path produced no plan and silently kept the static 60mm.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Bounds.Width", "30"));
        text.Bounds.Width.ToMm().Should().BeApproximately(30, 0.01, "a path through a struct segment now binds (Bounds.Width)");
    }

    [Fact]
    public async Task Binds_a_unit_with_a_dot_decimal_under_pt_BR_without_a_10x_error()
    {
        // Under pt-BR (the default expression culture) "2.5" must be 2.5mm, not 25 — NumberStyles.Any used
        // to read the '.' as a thousands separator. Invariant-first + no AllowThousands fixes the 10× bug.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Bounds.Width", "2.5"));
        // ~2.5mm (Unit's mil rounding makes it 2.489), the point being it is NOT 25mm (the old 10× bug).
        text.Bounds.Width.ToMm().Should().BeApproximately(2.5, 0.05, "the dot stays a decimal point, not grouping");
    }

    [Fact]
    public async Task An_out_of_range_enum_number_is_rejected_keeping_the_static_value()
    {
        // Enum.Parse accepts "99" without throwing; the binder must treat an undefined value as a coercion
        // failure (graceful fallback) instead of persisting a garbage enum that renders as the switch default.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Style.HorizontalAlignment", "99"));
        ((int)text.Style.HorizontalAlignment).Should().NotBe(99);
        Enum.IsDefined(text.Style.HorizontalAlignment).Should().BeTrue("an undefined enum number falls back to the static value");
    }

    [Fact]
    public async Task Binds_a_numeric_leaf_from_a_dot_decimal_string_without_a_10x_error()
    {
        // A STRING numeric ("2.5") bound to a double leaf must be 2.5, not 25 — the same pt-BR thousands
        // trap Unit had, now fixed for double/decimal/float too (the generic Convert.ChangeType used culture).
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Style.Font.Size", "'2.5'"));
        text.Style.Font.Size.Should().BeApproximately(2.5, 0.001, "the dot stays a decimal point, not grouping");
    }

    [Fact]
    public async Task A_numeric_result_bound_to_a_colour_is_rejected_keeping_the_static()
    {
        // 160000 must NOT be silently mis-read as the hex colour #160000 — a non-string result is no colour.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Style.ForeColor", "160000"));
        text.Style.ForeColor.Should().Be(Color.Black, "a numeric result is not a hex colour; static black survives");
    }

    [Fact]
    public async Task A_comma_list_bound_to_a_single_value_enum_is_rejected()
    {
        // "Left,Right" is only valid for a [Flags] enum; for HorizontalAlignment it must be rejected.
        var text = await RenderTextBox(new StyledRow("A", "#000000", 10), ("Style.HorizontalAlignment", "'Left,Right'"));
        Enum.IsDefined(text.Style.HorizontalAlignment).Should().BeTrue("a comma-list is invalid for a non-[Flags] enum; static value survives");
    }

    [Fact]
    public async Task The_Format_property_formats_a_single_value_textbox()
    {
        // "{Fields.Tamanho}" + Style.Format "C" must render as currency WITHOUT needing the inline {:C}.
        var tb = new TextBoxElement
        {
            Id = "t",
            Expression = "{Fields.Tamanho}",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
            Style = Style.Default with { Format = "C" },
        };
        var text = await RenderCustom(tb, new StyledRow("A", "#000000", 1234.5));
        text.Text.Should().Contain("R$", "the Format property formats the bound value as currency");
        text.Text.Should().NotBe("1234,5", "it is not the raw, unformatted number");
    }

    [Fact]
    public async Task An_inline_format_wins_over_the_Format_property()
    {
        var tb = new TextBoxElement
        {
            Id = "t",
            Expression = "{Fields.Tamanho:N0}",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
            Style = Style.Default with { Format = "C" },
        };
        var text = await RenderCustom(tb, new StyledRow("A", "#000000", 1234.5));
        text.Text.Should().NotContain("R$", "an inline :N0 takes precedence over the element Format");
    }

    [Fact]
    public async Task A_flat_tablix_cell_honours_the_Format_property()
    {
        // Same SSRS-style Format as a band textbox, now also on a flat Tablix detail cell.
        var detailCell = new TextBoxElement { Id = "c", Expression = "{Fields.Tamanho}", Style = Style.Default with { Format = "C" } };
        var tablix = new TablixElement
        {
            Id = "t",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 16.Mm()),
            Cells = new EquatableArray<TablixCell>(new[] { new TablixCell(1, 0, detailCell) }),
        };
        var detail = new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { tablix }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [new StyledRow("A", "#000000", 1234.5)]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });

        var texts = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain(t => t.Contains("R$"), "a flat Tablix cell formats via its Format property, like a band textbox");
    }

    [Fact]
    public async Task A_gauge_value_label_honours_the_Format_property()
    {
        var gauge = new GaugeElement
        {
            Id = "g",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 40.Mm()),
            ValueExpression = "Fields.Tamanho",
            MinimumExpression = "0",
            MaximumExpression = "2000",
            Style = Style.Default with { Format = "C" },
        };
        var detail = new DetailBand(45.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { gauge }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [new StyledRow("A", "#000000", 1234.5)]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });

        var texts = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain(t => t.Contains("R$"), "the gauge value label formats via its Format property");
    }

    [Fact]
    public async Task A_chart_value_axis_honours_the_Format_property()
    {
        var chart = new ChartElement
        {
            Id = "ch",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 50.Mm()),
            Kind = ChartKind.Bar,
            Series = new EquatableArray<ChartSeries>(new[] { new ChartSeries("S", "Fields.Nome", "Fields.Tamanho") }),
            Style = Style.Default with { Format = "C" },
        };
        var detail = new DetailBand(55.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { chart }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [new StyledRow("A", "#000000", 1000)]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });

        var texts = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain(t => t.Contains("R$"), "the chart value-axis labels format via the Format property");
    }

    [Fact]
    public async Task A_flat_tablix_cell_honours_its_content_style()
    {
        var detailCell = new TextBoxElement
        {
            Id = "c",
            Expression = "{Fields.Nome}",
            Style = Style.Default with { ForeColor = Color.Red, HorizontalAlignment = HorizontalAlignment.Right },
        };
        var tablix = new TablixElement
        {
            Id = "t",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 16.Mm()),
            Cells = new EquatableArray<TablixCell>(new[] { new TablixCell(1, 0, detailCell) }),
        };
        var detail = new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { tablix }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [new StyledRow("Ana", "#000000", 1)]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });

        var cell = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().First(t => t.Text == "Ana");
        cell.Style.ForeColor.Should().Be(Color.Red, "the flat cell honours its content's ForeColor");
        cell.Style.HorizontalAlignment.Should().Be(HorizontalAlignment.Right, "and its alignment, not a hardcoded default");
    }

    private static async Task<DrawTextPrimitive> RenderCustom(TextBoxElement tb, StyledRow row)
    {
        var detail = new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { tb }));
        var def = new ReportDefinition("e", PageSetup.A4Portrait, detail);
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [row]));
        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = def, DataSources = registry });
        return report.Pages[0].Primitives.OfType<DrawTextPrimitive>().First();
    }

    [Fact]
    public async Task An_unknown_path_is_ignored_and_the_static_value_is_kept()
    {
        var text = await RenderTextBox(new StyledRow("A", "#CC0000", 10), ("Style.NaoExiste", "Fields.Cor"));
        text.Style.ForeColor.Should().Be(Color.Black, "a bad path must never break the render — static value is the fallback");
    }

    [Fact]
    public async Task An_unevaluable_expression_is_ignored_and_the_static_value_is_kept()
    {
        var text = await RenderTextBox(new StyledRow("A", "#CC0000", 10), ("Style.ForeColor", "Fields.CampoInexistente"));
        text.Style.ForeColor.Should().Be(Color.Black);
    }
}
