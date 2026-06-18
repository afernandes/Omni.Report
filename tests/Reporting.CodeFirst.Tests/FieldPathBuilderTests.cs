using System.Linq.Expressions;
using FluentAssertions;
using Reporting.CodeFirst;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public class FieldPathBuilderTests
{
    public sealed record Venda(string Cliente, Endereco Address);
    public sealed record Endereco(string Cidade, string Estado);

    [Fact]
    public void Single_level_property_yields_simple_path()
    {
        Expression<Func<Venda, object>> selector = v => v.Cliente;
        FieldPathBuilder.From(selector).Should().Be("Fields.Cliente");
    }

    [Fact]
    public void Nested_property_yields_dotted_path()
    {
        Expression<Func<Venda, object>> selector = v => v.Address.Cidade;
        FieldPathBuilder.From(selector).Should().Be("Fields.Address.Cidade");
    }

    [Fact]
    public void Non_member_selector_throws()
    {
        Expression<Func<Venda, object>> selector = v => 1;
        Action act = () => FieldPathBuilder.From(selector);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Null_selector_throws()
    {
        Action act = () => FieldPathBuilder.From<Venda>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
