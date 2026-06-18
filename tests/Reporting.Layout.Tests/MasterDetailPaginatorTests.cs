using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>End-to-end tests covering the master-detail iteration the paginator runs when
/// a primary <see cref="DataSourceDefinition"/> has a <see cref="DataRelation"/>: for each
/// parent row, every matching child row produces one Detail-band iteration with both
/// source contexts (<c>{Fields.Parent.X}</c> and <c>{Fields.Child.Y}</c>) in scope.</summary>
public class MasterDetailPaginatorTests
{
    private sealed record Cliente(int id, string nome);
    private sealed record Pedido(int id, int cliente_id, string produto);

    [Fact]
    public async Task Master_detail_emits_one_detail_row_per_matching_child()
    {
        // Two parents — Ana has 2 pedidos, Beto has 1.
        var clientes = new[]
        {
            new Cliente(1, "Ana"),
            new Cliente(2, "Beto"),
        };
        var pedidos = new[]
        {
            new Pedido(101, 1, "Caneta"),
            new Pedido(102, 1, "Caderno"),
            new Pedido(103, 2, "Borracha"),
        };

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));
        registry.Register(new EnumerableDataSource<Pedido>("Pedidos", pedidos));

        var def = new ReportDefinition("MD", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                // Two fully-qualified references — proves the parent + child contexts are both live.
                MakeText("c", new Rectangle(Unit.FromMm(0), Unit.Zero, Unit.FromMm(40), Unit.FromMm(6)), "{Fields.Clientes.nome}"),
                MakeText("p", new Rectangle(Unit.FromMm(40), Unit.Zero, Unit.FromMm(60), Unit.FromMm(6)), "{Fields.Pedidos.produto}"),
            ])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("nome", typeof(string)),
                    ]),
                    Relations: new EquatableArray<DataRelation>([
                        new DataRelation("ClientesPedidos", "Clientes", "id", "Pedidos", "cliente_id"),
                    ])),
                new DataSourceDefinition("Pedidos",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("cliente_id", typeof(int)),
                        new DataField("produto", typeof(string)),
                    ])),
            ]),
        };

        var req = new PaginationRequest
        {
            Definition = def,
            DataSources = registry,
            PrimaryDataSource = "Clientes",
        };

        var report = await new ReportPaginator().PaginateAsync(req);

        // Extract emitted text in iteration order — one pair per (parent, child) match.
        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        // Expected: Ana,Caneta · Ana,Caderno · Beto,Borracha
        texts.Should().Equal("Ana", "Caneta", "Ana", "Caderno", "Beto", "Borracha");
    }

    [Fact]
    public async Task Single_source_iteration_still_works_without_relations()
    {
        // No relations — paginator falls back to single-source iteration. Verifies the
        // backwards-compatible path (the report has just one source, no relations declared).
        var clientes = new[] { new Cliente(1, "Ana"), new Cliente(2, "Beto") };
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));

        var def = new ReportDefinition("Solo", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                MakeText("n", new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(6)), "{Fields.nome}"),
            ])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("nome", typeof(string)),
                    ])),
            ]),
        };
        var req = new PaginationRequest { Definition = def, DataSources = registry };

        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        texts.Should().Equal("Ana", "Beto");
    }

    [Fact]
    public async Task Qualified_reference_on_primary_source_resolves_in_single_source_report()
    {
        // Even without master-detail, a qualified reference {Fields.Clientes.nome} must
        // resolve when "Clientes" is the primary source — the paginator publishes it as
        // a named source context per iteration.
        var clientes = new[] { new Cliente(1, "Ana") };
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));

        var def = new ReportDefinition("Q", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                MakeText("q", new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(6)), "{Fields.Clientes.nome}"),
            ])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([
                        new DataField("nome", typeof(string)),
                    ])),
            ]),
        };
        var req = new PaginationRequest { Definition = def, DataSources = registry, PrimaryDataSource = "Clientes" };

        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        texts.Should().Equal("Ana");
    }

    [Fact]
    public async Task Parent_without_matching_children_produces_no_detail_rows_inner_join_semantics()
    {
        // Default behavior is inner-join: parents whose key doesn't match any child are
        // skipped. Documents the current contract — switch to outer-join would require
        // an explicit opt-in flag on the relation (future work).
        var clientes = new[] { new Cliente(1, "Ana"), new Cliente(2, "Beto"), new Cliente(3, "Lonely") };
        var pedidos = new[] { new Pedido(1, 1, "Caneta") };

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));
        registry.Register(new EnumerableDataSource<Pedido>("Pedidos", pedidos));

        var def = new ReportDefinition("MD", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                MakeText("c", new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(6)), "{Fields.Clientes.nome}"),
            ])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([new DataField("id", typeof(int)), new DataField("nome", typeof(string))]),
                    Relations: new EquatableArray<DataRelation>([
                        new DataRelation("R", "Clientes", "id", "Pedidos", "cliente_id"),
                    ])),
                new DataSourceDefinition("Pedidos",
                    Fields: new EquatableArray<DataField>([new DataField("cliente_id", typeof(int))])),
            ]),
        };
        var req = new PaginationRequest { Definition = def, DataSources = registry, PrimaryDataSource = "Clientes" };
        var report = await new ReportPaginator().PaginateAsync(req);

        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        // Only Ana shows up — Beto and Lonely have no matching pedidos.
        texts.Should().Equal("Ana");
    }

    [Fact]
    public async Task SubDetail_emits_parent_once_then_child_rows_with_parent_context_in_scope()
    {
        var clientes = new[] { new Cliente(1, "Ana"), new Cliente(2, "Beto") };
        var pedidos = new[]
        {
            new Pedido(101, 1, "Caneta"),
            new Pedido(102, 1, "Caderno"),
            new Pedido(103, 2, "Borracha"),
        };

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));
        registry.Register(new EnumerableDataSource<Pedido>("Pedidos", pedidos));

        // Detail iterates Clientes (one row per cliente); SubDetail iterates Pedidos filtered
        // by the relation. Expected emit order:
        //   "Cliente: Ana", "  · Caneta", "  · Caderno",
        //   "Cliente: Beto", "  · Borracha"
        var sub = new SubDetailBand(
            Name: "PedidosDoCliente",
            DataMember: "R",  // relation name declared below
            Height: Unit.FromMm(5),
            Elements: new EquatableArray<ReportElement>([
                MakeText("pl", new Rectangle(Unit.FromMm(10), Unit.Zero, Unit.FromMm(80), Unit.FromMm(5)),
                    "  · {Fields.produto}"),
            ]));

        var def = new ReportDefinition("MD-Sub", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                MakeText("c", new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(6)),
                    "Cliente: {Fields.nome}"),
            ]), SubDetails: new EquatableArray<SubDetailBand>([sub])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("nome", typeof(string)),
                    ]),
                    Relations: new EquatableArray<DataRelation>([
                        new DataRelation("R", "Clientes", "id", "Pedidos", "cliente_id"),
                    ])),
                new DataSourceDefinition("Pedidos",
                    Fields: new EquatableArray<DataField>([
                        new DataField("cliente_id", typeof(int)),
                        new DataField("produto", typeof(string)),
                    ])),
            ]),
        };
        var req = new PaginationRequest { Definition = def, DataSources = registry, PrimaryDataSource = "Clientes" };
        var report = await new ReportPaginator().PaginateAsync(req);

        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        texts.Should().Equal(
            "Cliente: Ana",   "  · Caneta", "  · Caderno",
            "Cliente: Beto",  "  · Borracha");
    }

    [Fact]
    public async Task Unqualified_parent_field_resolves_in_child_iteration_via_master_detail_fallback()
    {
        // Crystal Reports / FastReport UX: when the user drops a parent field into the detail
        // band, they don't qualify it (Fields.nome, not Fields.Clientes.nome). Master-detail
        // iteration makes the CHILD the live row, so the parent field must resolve via the
        // cross-source fallback in IReportExpressionContext.TryResolveUnqualifiedField.
        var clientes = new[] { new Cliente(1, "Ana"), new Cliente(2, "Beto") };
        var pedidos = new[]
        {
            new Pedido(101, 1, "Caneta"),
            new Pedido(102, 2, "Borracha"),
        };

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Cliente>("Clientes", clientes));
        registry.Register(new EnumerableDataSource<Pedido>("Pedidos", pedidos));

        var def = new ReportDefinition("MD-unqualified", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([
                // Both UNQUALIFIED: "nome" lives on the parent (Clientes), "produto" on the
                // child (Pedidos). The fallback must find each in the right source.
                MakeText("c", new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(6)), "{Fields.nome}"),
                MakeText("p", new Rectangle(Unit.FromMm(40), Unit.Zero, Unit.FromMm(60), Unit.FromMm(6)), "{Fields.produto}"),
            ])))
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Clientes",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("nome", typeof(string)),
                    ]),
                    Relations: new EquatableArray<DataRelation>([
                        new DataRelation("R", "Clientes", "id", "Pedidos", "cliente_id"),
                    ])),
                new DataSourceDefinition("Pedidos",
                    Fields: new EquatableArray<DataField>([
                        new DataField("id", typeof(int)),
                        new DataField("cliente_id", typeof(int)),
                        new DataField("produto", typeof(string)),
                    ])),
            ]),
        };

        var req = new PaginationRequest { Definition = def, DataSources = registry, PrimaryDataSource = "Clientes" };
        var report = await new ReportPaginator().PaginateAsync(req);

        var texts = report.Pages.SelectMany(p => p.Primitives.OfType<DrawTextPrimitive>())
                                .Select(t => t.Text).ToList();
        // Expected: every detail row shows BOTH the parent's nome AND the child's produto,
        // even though neither reference is qualified.
        texts.Should().Equal("Ana", "Caneta", "Beto", "Borracha");
    }

    private static TextBoxElement MakeText(string id, Rectangle bounds, string expr) =>
        new()
        {
            Id = id,
            Bounds = bounds,
            Expression = expr,
        };
}
