using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Xunit;

namespace Reporting.DataSources.Tests;

public class DataSourceRegistryTests
{
    [Fact]
    public void Registers_and_retrieves_by_name()
    {
        var registry = new DataSourceRegistry();
        var ds = new EnumerableDataSource<Venda>("Vendas", Array.Empty<Venda>());
        registry.Register(ds);
        registry.Get("Vendas").Should().BeSameAs(ds);
        registry.TryGet("vendas", out var found).Should().BeTrue();
        found.Should().BeSameAs(ds);
        registry.Names.Should().Contain("Vendas");
    }

    [Fact]
    public void Throws_when_name_missing()
    {
        var registry = new DataSourceRegistry();
        Action act = () => registry.Get("absent");
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*absent*");
    }

    [Fact]
    public void Removes_existing_source()
    {
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Venda>("Vendas", []));
        registry.Remove("Vendas").Should().BeTrue();
        registry.Remove("Vendas").Should().BeFalse();
    }
}
