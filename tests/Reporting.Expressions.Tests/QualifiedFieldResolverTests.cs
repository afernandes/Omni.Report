using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// Covers the qualified-source field resolution: <c>{Fields.SourceName.Field}</c> looks up
/// the named source's current row, while <c>{Fields.Field}</c> keeps using the active source.
/// Mirrors how Crystal / SSRS / DevExpress disambiguate same-named fields across data sources.
/// </summary>
public sealed class QualifiedFieldResolverTests
{
    private readonly ExpressionEvaluator _eval = new();

    [Fact]
    public void Unqualified_field_uses_active_source()
    {
        var ctx = new ReportExpressionContext();
        ctx.SetCurrentRow([new("id", 42), new("nome", "Active")]);

        _eval.Evaluate("Fields.id", ctx).Should().Be(42);
        _eval.Evaluate("Fields.nome", ctx).Should().Be("Active");
    }

    [Fact]
    public void Qualified_field_resolves_against_named_source()
    {
        var ctx = new ReportExpressionContext();
        // Active source = Pedidos (the iterating one).
        ctx.SetCurrentRow([new("id", 99), new("cliente_id", 1)]);
        ctx.SetSourceCurrentRow("Pedidos", new[]
        {
            new KeyValuePair<string, object?>("id", 99),
            new KeyValuePair<string, object?>("cliente_id", 1),
        });
        // Master source kept in scope so the detail band can reach back.
        ctx.SetSourceCurrentRow("Clientes", new[]
        {
            new KeyValuePair<string, object?>("id", 1),
            new KeyValuePair<string, object?>("nome", "Ana Beatriz"),
        });

        _eval.Evaluate("Fields.Pedidos.id", ctx).Should().Be(99);
        _eval.Evaluate("Fields.Clientes.id", ctx).Should().Be(1);
        _eval.Evaluate("Fields.Clientes.nome", ctx).Should().Be("Ana Beatriz");
    }

    [Fact]
    public void Qualified_form_takes_precedence_over_unqualified_with_same_head()
    {
        // Edge case: a source named "Cliente" exists AND the active row has a field called
        // "Cliente" with a nested object. Source wins (qualified is explicit).
        var ctx = new ReportExpressionContext();
        ctx.SetCurrentRow([new("Cliente", new Dictionary<string, object?> { ["Nome"] = "Active" })]);
        ctx.SetSourceCurrentRow("Cliente", new[]
        {
            new KeyValuePair<string, object?>("Nome", "FromSource"),
        });

        _eval.Evaluate("Fields.Cliente.Nome", ctx).Should().Be("FromSource");
    }

    [Fact]
    public void Unknown_qualified_source_returns_null_not_throws()
    {
        var ctx = new ReportExpressionContext();
        ctx.SetCurrentRow([new("nome", "Active")]);

        var result = _eval.Evaluate("Fields.UnknownSource.field", ctx);
        result.Should().BeNull();
    }

    [Fact]
    public void Backwards_compatible_nested_member_resolution_still_works()
    {
        // No source called "Endereco" — the dotted chain falls back to member resolution on
        // the active row's "Endereco" object (a POCO with public properties).
        var ctx = new ReportExpressionContext();
        ctx.SetCurrentRow([new("Endereco", new Endereco("Av. Paulista", 1000))]);

        _eval.Evaluate("Fields.Endereco.Rua", ctx).Should().Be("Av. Paulista");
        _eval.Evaluate("Fields.Endereco.Numero", ctx).Should().Be(1000);
    }

    private sealed record Endereco(string Rua, int Numero);

    [Fact]
    public void ClearSourceCurrentRow_removes_qualified_access()
    {
        var ctx = new ReportExpressionContext();
        ctx.SetSourceCurrentRow("Pedidos", new[]
        {
            new KeyValuePair<string, object?>("id", 7),
        });
        _eval.Evaluate("Fields.Pedidos.id", ctx).Should().Be(7);

        ctx.ClearSourceCurrentRow("Pedidos");
        _eval.Evaluate("Fields.Pedidos.id", ctx).Should().BeNull();
    }
}
