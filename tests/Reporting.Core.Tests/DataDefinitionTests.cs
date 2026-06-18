using FluentAssertions;
using Reporting.Common;
using Reporting.Data;
using Xunit;

namespace Reporting.Core.Tests;

public class DataDefinitionTests
{
    [Fact]
    public void DataSourceDefinition_with_fields_and_relations()
    {
        var ds = new DataSourceDefinition(
            "Vendas",
            DataMember: "dbo.vw_vendas",
            Fields: EquatableArray.Create(
                new DataField("Cliente", typeof(string), "Cliente"),
                new DataField("Total", typeof(decimal))),
            Relations: EquatableArray.Create(
                new DataRelation("VendasItens", "Vendas", "Id", "Itens", "VendaId")),
            Parameters: new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["since"] = "Parameters.DataInicio" }));

        ds.Name.Should().Be("Vendas");
        ds.DataMember.Should().Be("dbo.vw_vendas");
        ds.Fields.Count.Should().Be(2);
        ds.Fields[0].Name.Should().Be("Cliente");
        ds.Fields[0].DisplayName.Should().Be("Cliente");
        ds.Fields[1].FieldType.Should().Be(typeof(decimal));
        ds.Relations.Count.Should().Be(1);
        ds.Relations[0].ParentField.Should().Be("Id");
        ds.Parameters["since"].Should().Be("Parameters.DataInicio");
    }

    [Fact]
    public void Two_definitions_with_same_content_are_equal()
    {
        var a = new DataSourceDefinition("X");
        var b = new DataSourceDefinition("X");
        a.Should().Be(b);
    }
}
