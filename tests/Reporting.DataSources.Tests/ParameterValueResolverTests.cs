using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Parameters;
using Xunit;

namespace Reporting.DataSources.Tests;

public class ParameterValueResolverTests
{
    private sealed record Cliente(int Id, string Nome);

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
    public async Task Unknown_dataset_yields_only_the_static_values()
    {
        var available = ParameterAvailableValues.FromQuery("NaoExiste", "Id");

        var result = await ParameterValueResolver.ResolveAsync(available, new DataSourceRegistry());

        result.Should().BeEmpty();
    }
}
