using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record LkpPedido(string Produto, int ClienteId);
public sealed record LkpCliente(int Id, string Nome);

/// <summary>
/// End-to-end proof that <c>Lookup</c> resolves across datasets in a real report: the paginator registers
/// every dataset's rows, so a Detail bound to "Pedidos" can pull the customer name from "Clientes".
/// </summary>
public class LookupEndToEndTests
{
    [Fact]
    public async Task Lookup_resolves_a_value_from_a_second_dataset_during_render()
    {
        var pedidos = new[] { new LkpPedido("Caneta", 1), new LkpPedido("Caderno", 2) };
        var clientes = new[] { new LkpCliente(1, "Ana"), new LkpCliente(2, "Bia") };

        var report = ReportBuilder.Create("Lookup")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Pedidos", pedidos)   // primary — the Detail iterates this
            .DataSource("Clientes", clientes) // lookup target
            .Detail(d => d.Height(6)
                .Text("{Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')}").At(0, 0).Size(60, 6))
            .Build();

        var texts = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        texts.Should().Contain("Ana", "pedido of ClienteId 1 looks up customer Ana");
        texts.Should().Contain("Bia", "pedido of ClienteId 2 looks up customer Bia");
    }
}
