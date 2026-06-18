using System.Text;
using FluentAssertions;
using Reporting;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>End-to-end round-trip tests. Each format must satisfy
/// <c>Load(Save(def)).Equals(def)</c> for any valid definition.</summary>
public class RoundTripTests
{
    public static TheoryData<IReportSerializer> Serializers => new()
    {
        new RepxSerializer(),
        new RepJsonSerializer(),
    };

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Minimal_report_round_trips(IReportSerializer serializer)
    {
        var original = Fixtures.MinimalReport();
        var bytes = serializer.SaveToBytes(original);
        var loaded = serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Kitchen_sink_definition_round_trips(IReportSerializer serializer)
    {
        var original = Fixtures.KitchenSink();
        var bytes = serializer.SaveToBytes(original);
        var loaded = serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Sample01_vendas_por_cliente_round_trips(IReportSerializer serializer)
    {
        var original = Samples.CodeFirst.Reports.Sample01_VendasPorCliente.Build().Definition;
        var bytes = serializer.SaveToBytes(original);
        var loaded = serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Sample02_espelho_produtos_round_trips(IReportSerializer serializer)
    {
        var original = Samples.CodeFirst.Reports.Sample02_EspelhoProdutos.Build().Definition;
        var bytes = serializer.SaveToBytes(original);
        var loaded = serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Sample03_relatorio_caixa_round_trips(IReportSerializer serializer)
    {
        var original = Samples.CodeFirst.Reports.Sample03_RelatorioCaixa.Build().Definition;
        var bytes = serializer.SaveToBytes(original);
        var loaded = serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Twice_serializing_produces_byte_identical_output(IReportSerializer serializer)
    {
        var original = Fixtures.KitchenSink();
        var first = serializer.SaveToBytes(original);
        var second = serializer.SaveToBytes(original);
        first.Should().Equal(second);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void Load_save_load_is_idempotent(IReportSerializer serializer)
    {
        var original = Fixtures.KitchenSink();
        var pass1 = serializer.LoadFromBytes(serializer.SaveToBytes(original));
        var pass2 = serializer.LoadFromBytes(serializer.SaveToBytes(pass1));
        pass1.Should().Be(pass2);
    }
}
