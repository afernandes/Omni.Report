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
