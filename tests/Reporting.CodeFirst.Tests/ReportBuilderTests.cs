using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Reporting.Styling;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record Venda(string Cliente, decimal Total);

public class ReportBuilderTests
{
    [Fact]
    public void Create_with_minimum_settings_builds_valid_definition()
    {
        var report = ReportBuilder.Create("Test").Build();
        report.Definition.Name.Should().Be("Test");
        report.Definition.Detail.Should().NotBeNull();
    }

    [Fact]
    public void Page_setup_is_applied()
    {
        var report = ReportBuilder.Create("Test")
            .Page(p => p.A5().Landscape().Margins(10))
            .Build();
        report.Definition.PageSetup.Paper.Name.Should().Be("A5");
        report.Definition.PageSetup.Orientation.Should().Be(Reporting.Paper.Orientation.Landscape);
    }

    [Fact]
    public void Parameters_are_added_with_metadata()
    {
        var report = ReportBuilder.Create("Test")
            .Parameters(p => p
                .Add<DateTime>("Start", prompt: "Início", defaultValue: new DateTime(2026, 1, 1))
                .Add<int>("Top", defaultValue: 10))
            .Build();
        report.Definition.Parameters.Count.Should().Be(2);
        report.Definition.Parameters[0].Name.Should().Be("Start");
        report.Definition.Parameters[0].Prompt.Should().Be("Início");
        report.Definition.Parameters[0].ValueType.Should().Be(typeof(DateTime));
        report.Definition.Parameters[1].DefaultValue.Should().Be(10);
    }

    [Fact]
    public void Parameter_available_values_flow_through_the_builder()
    {
        var report = ReportBuilder.Create("Test")
            .Parameters(p => p
                .Add<string>("Status", availableValues: Reporting.Parameters.ParameterAvailableValues.FromList(
                    new Reporting.Parameters.ParameterValue("A", "Ativo"),
                    new Reporting.Parameters.ParameterValue("I", "Inativo")))
                .Add<string>("Cliente", availableValues: Reporting.Parameters.ParameterAvailableValues.FromQuery(
                    "Clientes", "Id", "Nome")))
            .Build();

        var status = report.Definition.Parameters[0].AvailableValues;
        status.Should().NotBeNull();
        status!.Values.Select(v => v.Value).Should().Equal("A", "I");
        status.Values[0].Label.Should().Be("Ativo");

        var cliente = report.Definition.Parameters[1].AvailableValues;
        cliente!.IsQuery.Should().BeTrue();
        cliente.DataSet.Should().Be("Clientes");
        cliente.ValueField.Should().Be("Id");
        cliente.LabelField.Should().Be("Nome");
    }

    [Fact]
    public void Datasource_registers_and_exposes_fields()
    {
        var rows = new[] { new Venda("Ana", 10m), new Venda("Beto", 20m) };
        var report = ReportBuilder.Create("Test")
            .DataSource("Vendas", rows)
            .Build();

        report.DataSources.TryGet("Vendas", out var ds).Should().BeTrue();
        ds.Schema.Fields.Select(f => f.Name).Should().Contain(["Cliente", "Total"]);
        report.Definition.DataSources.Should().HaveCount(1);
        report.Definition.DataSources[0].Fields.Select(f => f.Name).Should().Contain("Cliente");
    }

    [Fact]
    public async Task End_to_end_paginate_via_builder()
    {
        var rows = new[] { new Venda("Ana", 10m), new Venda("Ana", 5m), new Venda("Beto", 3m) };
        var report = ReportBuilder.Create("E2E")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", rows)
            .Detail(d => d.Height(6).Text("{Fields.Cliente}").At(0, 0).Size(50, 6))
            .Build();

        var rendered = await report.PaginateAsync();
        rendered.Pages.Should().HaveCount(1);
        var texts = rendered.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain("Ana");
        texts.Should().Contain("Beto");
    }

    [Fact]
    public void Group_with_string_expression_builds_groupband()
    {
        var report = ReportBuilder.Create("Grouped")
            .DataSource("V", new[] { new Venda("A", 1m) })
            .Group("g", "Fields.Cliente", g => g
                .Header(h => h.Height(5).Label("hdr"))
                .Detail(d => d.Height(5).Text("{Fields.Cliente}"))
                .Footer(f => f.Height(5).Label("ftr"))
                .KeepGroupTogether().RepeatHeader())
            .Build();
        report.Definition.Groups.Should().HaveCount(1);
        report.Definition.Groups[0].KeepTogether.Should().BeTrue();
        report.Definition.Groups[0].RepeatHeaderOnNewPage.Should().BeTrue();
        report.Definition.Detail.Elements.Should().NotBeEmpty();
    }

    [Fact]
    public void Group_with_typed_selector_converts_to_string_path()
    {
        var report = ReportBuilder.Create("Typed")
            .DataSource("V", new[] { new Venda("X", 1m) })
            .Group<Venda>("g", v => v.Cliente, g => g
                .Detail(d => d.Height(5).Text("{Fields.Cliente}")))
            .Build();
        report.Definition.Groups[0].GroupExpression.Should().Be("Fields.Cliente");
    }

    [Fact]
    public void Bands_capture_all_kinds()
    {
        var report = ReportBuilder.Create("All")
            .ReportHeader(b => b.Height(5).Label("rh"))
            .PageHeader(b => b.Height(5).Label("ph"))
            .Detail(b => b.Height(5).Label("d"))
            .PageFooter(b => b.Height(5).Label("pf"))
            .ReportFooter(b => b.Height(5).Label("rf"))
            .Build();
        report.Definition.ReportHeader.Should().NotBeNull();
        report.Definition.PageHeader.Should().NotBeNull();
        report.Definition.PageFooter.Should().NotBeNull();
        report.Definition.ReportFooter.Should().NotBeNull();
        report.Definition.Detail.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void Metadata_is_preserved()
    {
        var report = ReportBuilder.Create("M")
            .Metadata("Author", "ana")
            .Metadata("Version", "0.1")
            .Build();
        report.Definition.Metadata["Author"].Should().Be("ana");
        report.Definition.Metadata["Version"].Should().Be("0.1");
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var rb = ReportBuilder.Create("X");
        ((Action)(() => rb.Page(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => rb.Parameters(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => rb.DataSource<Venda>("V", null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => rb.ReportHeader(null!))).Should().Throw<ArgumentNullException>();
    }
}
