# Data Sources

Toda fonte de dados implementa `IReportDataSource`, expondo enumeração de
`IReportRecord` (mapa nome → valor). O motor é agnóstico ao backing store:
in-memory, `DataTable`, EF Core, dapper, REST — qualquer coisa que produza linhas.

## Built-in

### `EnumerableDataSource<T>`

Adapta `IEnumerable<T>` usando acessores compilados (cache por tipo, gerados via
`Expression<Func<T, object>>`). Acesso por propriedade é tão rápido quanto leitura
direta — sem reflexão por linha.

```csharp
public record Venda(string Cliente, decimal Total, DateTime Data);

var vendas = new[]
{
    new Venda("Acme", 1234.56m, new DateTime(2026, 5, 1)),
    new Venda("Globex", 980.10m, new DateTime(2026, 5, 2)),
};

var source = new EnumerableDataSource<Venda>("Vendas", vendas);
```

Nas expressões: `Fields.Cliente`, `Fields.Total`, `Fields.Data`.

Suporta tipos aninhados — acesso ponto a ponto via `MemberPathResolver`:

```csharp
public record Pedido(Cliente Cliente, decimal Total);
public record Cliente(string Nome, Endereco Endereco);
public record Endereco(string Cidade, string Uf);

// expressões: Fields.Cliente.Nome, Fields.Cliente.Endereco.Cidade
```

### `DataTableDataSource`

Para apps legados / ADO.NET:

```csharp
DataTable dt = LoadFromSqlServer();
var source = new DataTableDataSource("Vendas", dt);
```

Colunas viram `Fields.<NomeColuna>`.

## `DataSourceRegistry`

Centraliza fontes nomeadas. O `ReportingBuilder` (host AspNetCore) já expõe um
registry compartilhado:

```csharp
services.AddReporting(opts => opts
    .AddDataSource("Vendas", vendas)
    .AddDataSource("Produtos", produtos));
```

Em runtime, recupere via DI:

```csharp
public sealed class ReportsController(DataSourceRegistry sources, IReportPaginator paginator)
{
    public async Task<IActionResult> Vendas()
    {
        var def = ReportBuilder.Create("Vendas").Build();
        var ctx = new ReportExpressionContext();
        var source = sources.Get("Vendas");
        var pages = await paginator.PaginateAsync(def, source, ctx);
        // ...
    }
}
```

## Implementando uma fonte customizada

```csharp
public sealed class HttpJsonDataSource(string name, HttpClient http, string url)
    : IReportDataSource
{
    public string Name => name;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = await http.GetStreamAsync(url, ct);
        var rows = JsonSerializer.DeserializeAsyncEnumerable<Dictionary<string, object?>>(
            stream, cancellationToken: ct);

        await foreach (var row in rows.WithCancellation(ct))
        {
            if (row is null) continue;
            yield return new DictionaryRecord(row);
        }
    }
}
```

`DictionaryRecord` é uma implementação utilitária já disponível em
`Reporting.DataSources`.

## Performance

- Acessores são compilados uma vez por `T` e cacheados em `ConcurrentDictionary`.
- 100k registros são iterados em < 200ms em benchmark (ver `BenchmarkDotNet` em testes).
- Para datasets gigantes, prefira `IAsyncEnumerable` — o paginator consome streaming
  e não materializa tudo na memória.

## Subreports

Subreport é um elemento (`SubreportElement`) com sua própria fonte de dados,
resolvida em runtime. O parâmetro `DataSourceName` aponta para um nome registrado;
o paginator carrega e renderiza recursivamente, respeitando `MaxSubreportDepth = 4`.
