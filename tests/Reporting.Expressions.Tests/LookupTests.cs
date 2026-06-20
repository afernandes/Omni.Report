using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// SSRS-style cross-dataset <c>Lookup</c>/<c>LookupSet</c>: the source is evaluated in the caller's row
/// scope and matched against each row of a registered dataset; the result is read from the matched row.
/// </summary>
public class LookupTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) NewContext()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        // A "Clientes" dataset: Id → Nome. Two rows share category "VIP" for the LookupSet case.
        ctx.RegisterDataset("Clientes", new[]
        {
            new[] { new KeyValuePair<string, object?>("Id", 1), new KeyValuePair<string, object?>("Nome", "Ana"), new KeyValuePair<string, object?>("Cat", "VIP") },
            new[] { new KeyValuePair<string, object?>("Id", 2), new KeyValuePair<string, object?>("Nome", "Bia"), new KeyValuePair<string, object?>("Cat", "VIP") },
            new[] { new KeyValuePair<string, object?>("Id", 3), new KeyValuePair<string, object?>("Nome", "Cau"), new KeyValuePair<string, object?>("Cat", "Reg") },
        });
        return (ctx, ev);
    }

    [Fact]
    public void Lookup_returns_the_matched_rows_value()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["ClienteId"] = 2 });
        ev.Evaluate("Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')", ctx).Should().Be("Bia");
    }

    [Fact]
    public void Lookup_returns_null_when_no_row_matches()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["ClienteId"] = 99 });
        ev.Evaluate("Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')", ctx).Should().BeNull();
    }

    [Fact]
    public void Lookup_matches_across_differing_key_types()
    {
        // Current row has a STRING "1"; the dataset Id is an INT 1 — the invariant string fallback matches.
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["ClienteId"] = "1" });
        ev.Evaluate("Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')", ctx).Should().Be("Ana");
    }

    [Fact]
    public void LookupSet_returns_every_match()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Categoria"] = "VIP" });
        var result = ev.Evaluate("LookupSet(Fields.Categoria, Fields.Cat, Fields.Nome, 'Clientes')", ctx);
        result.Should().BeAssignableTo<object?[]>();
        ((object?[])result!).Should().Equal("Ana", "Bia");
    }

    [Fact]
    public void Lookup_does_not_disturb_the_current_row()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["ClienteId"] = 2, ["Produto"] = "Caneta" });
        ev.Evaluate("Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')", ctx);
        // After the lookup swapped rows internally, the caller's row must be restored.
        ev.Evaluate("Fields.Produto", ctx).Should().Be("Caneta");
    }

    [Fact]
    public void LookupSet_returns_an_empty_array_when_nothing_matches()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Categoria"] = "Inexistente" });
        var result = ev.Evaluate("LookupSet(Fields.Categoria, Fields.Cat, Fields.Nome, 'Clientes')", ctx);
        result.Should().BeAssignableTo<object?[]>();
        ((object?[])result!).Should().BeEmpty();
    }

    [Fact]
    public void Lookup_does_not_false_match_a_bool_against_its_string_form()
    {
        // Cross-type fallback is numeric-only: bool true must NOT match the literal string "True".
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.RegisterDataset("Flags", new[]
        {
            new[] { new KeyValuePair<string, object?>("Texto", "True"), new KeyValuePair<string, object?>("Nome", "x") },
        });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Ativo"] = true });
        ev.Evaluate("Lookup(Fields.Ativo, Fields.Texto, Fields.Nome, 'Flags')", ctx).Should().BeNull();
    }

    [Fact]
    public void Lookup_nests_correctly_inside_an_aggregate()
    {
        // Proves the row-swap restore nests with the aggregate row-walk: Sum evaluates a per-row Lookup
        // (each report row's Cod → its Preco) and totals them. 10 + 25 = 35.
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.RegisterDataset("Precos", new[]
        {
            new[] { new KeyValuePair<string, object?>("Cod", 1), new KeyValuePair<string, object?>("Preco", 10m) },
            new[] { new KeyValuePair<string, object?>("Cod", 2), new KeyValuePair<string, object?>("Preco", 25m) },
        });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Cod"] = 1 });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Cod"] = 2 });
        var total = ev.Evaluate("Sum(Lookup(Fields.Cod, Fields.Cod, Fields.Preco, 'Precos'))", ctx);
        Convert.ToDecimal(total).Should().Be(35m);
    }
}
