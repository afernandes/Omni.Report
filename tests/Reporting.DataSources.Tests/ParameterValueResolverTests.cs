using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Parameters;
using Xunit;

namespace Reporting.DataSources.Tests;

public class ParameterValueResolverTests
{
    private sealed record Cliente(int Id, string? Nome);

    private static DataSourceRegistry WithClientes(params Cliente[] clientes)
    {
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));
        return registry;
    }

    [Fact]
    public async Task Static_values_are_returned_as_is()
    {
        var available = ParameterAvailableValues.FromList(
            new ParameterValue("A", "Ativo"), new ParameterValue("I", "Inativo"));

        var result = await ParameterValueResolver.ResolveAsync(available, new DataSourceRegistry());

        result.Should().HaveCount(2);
        result[0].Value.Should().Be("A");
        result[0].Label.Should().Be("Ativo");
    }

    [Fact]
    public async Task Query_values_are_pulled_from_the_dataset_with_label_field()
    {
        var available = ParameterAvailableValues.FromQuery("Clientes", "Id", "Nome");
        var registry = WithClientes(new Cliente(1, "Ana"), new Cliente(2, "Bia"));

        var result = await ParameterValueResolver.ResolveAsync(available, registry);

        result.Select(v => v.Value).Should().Equal("1", "2");
        result.Select(v => v.Label).Should().Equal("Ana", "Bia");
    }

    [Fact]
    public async Task Query_values_are_distinct_by_value_in_first_seen_order()
    {
        var available = ParameterAvailableValues.FromQuery("Clientes", "Nome");
        var registry = WithClientes(new Cliente(1, "Ana"), new Cliente(2, "Bia"), new Cliente(3, "Ana"));

        var result = await ParameterValueResolver.ResolveAsync(available, registry);

        result.Select(v => v.Value).Should().Equal("Ana", "Bia"); // duplicate "Ana" collapsed
    }

    [Fact]
    public async Task Static_and_query_combine_static_first()
    {
        var available = ParameterAvailableValues.FromQuery("Clientes", "Nome") with
        {
            Values = new Reporting.Common.EquatableArray<ParameterValue>(
                [new ParameterValue("(todos)", "Todos")]),
        };
        var registry = WithClientes(new Cliente(1, "Ana"));

        var result = await ParameterValueResolver.ResolveAsync(available, registry);

        result.Select(v => v.Value).Should().Equal("(todos)", "Ana");
    }

    [Fact]
    public async Task Query_label_falls_back_to_null_when_the_label_cell_is_empty()
    {
        var available = ParameterAvailableValues.FromQuery("Clientes", "Id", "Nome");
        var registry = WithClientes(new Cliente(1, null)); // label cell is null

        var result = await ParameterValueResolver.ResolveAsync(available, registry);

        result.Should().ContainSingle();
        result[0].Value.Should().Be("1");
        result[0].Label.Should().BeNull("a null label cell falls back to the value downstream");
    }

    [Fact]
    public async Task Unknown_dataset_yields_only_the_static_values()
    {
        var available = ParameterAvailableValues.FromQuery("NaoExiste", "Id");

        var result = await ParameterValueResolver.ResolveAsync(available, new DataSourceRegistry());

        result.Should().BeEmpty();
    }

    // ── Cascading (dependent) parameters ──────────────────────────────────────────

    private sealed record Cidade(string Nome, string Estado);

    private static DataSourceRegistry WithCidades(params Cidade[] cidades)
    {
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cidade>("Cidades", cidades));
        return registry;
    }

    [Fact]
    public async Task Cascading_restricts_rows_to_the_parent_parameter_value()
    {
        var available = ParameterAvailableValues.FromCascadingQuery("Cidades", "Nome", filterField: "Estado", dependsOn: "Estado");
        var registry = WithCidades(new Cidade("Porto Alegre", "RS"), new Cidade("Curitiba", "PR"), new Cidade("Caxias", "RS"));

        var rs = await ParameterValueResolver.ResolveAsync(available, registry,
            new Dictionary<string, object?> { ["Estado"] = "RS" });
        rs.Select(v => v.Value).Should().BeEquivalentTo(new[] { "Porto Alegre", "Caxias" });

        var pr = await ParameterValueResolver.ResolveAsync(available, registry,
            new Dictionary<string, object?> { ["Estado"] = "PR" });
        pr.Select(v => v.Value).Should().Equal("Curitiba");
    }

    [Fact]
    public async Task Cascading_with_no_parent_value_yields_nothing()
    {
        var available = ParameterAvailableValues.FromCascadingQuery("Cidades", "Nome", "Estado", "Estado");
        var registry = WithCidades(new Cidade("Porto Alegre", "RS"));

        // The parent hasn't been chosen yet → the dependent list is empty until it is.
        var result = await ParameterValueResolver.ResolveAsync(available, registry, parameterValues: null);
        result.Should().BeEmpty();
    }
}
