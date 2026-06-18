namespace Reporting.Samples.CodeFirst.Data;

/// <summary>Sales row used by Sample 01.</summary>
public sealed record Venda(
    string Cliente,
    string Produto,
    int Quantidade,
    decimal PrecoUnitario,
    DateTime DataVenda)
{
    public decimal Total => Quantidade * PrecoUnitario;
}

/// <summary>Product master record used by Sample 02 (espelho de produtos).</summary>
public sealed record Produto(
    string Codigo,
    string Descricao,
    string Ean13,
    decimal PrecoVarejo,
    decimal PrecoAtacado);

/// <summary>Cashier movement row used by Sample 03 (movimento de caixa).</summary>
public sealed record CaixaMovimento(
    DateTime Data,
    string Documento,
    string FormaPagamento,
    decimal Valor);

public static class SampleData
{
    public static IReadOnlyList<Venda> Vendas() =>
    [
        new("Ana Beatriz",     "Caneta Bic Azul",            10, 2.50m,  new DateTime(2026, 5, 1)),
        new("Ana Beatriz",     "Caderno Brochura",            1, 27.40m, new DateTime(2026, 5, 3)),
        new("Ana Beatriz",     "Marcador de Texto",           3, 11.90m, new DateTime(2026, 5, 5)),
        new("Beto Silva",      "Caneta Bic Vermelha",         5, 2.50m,  new DateTime(2026, 5, 2)),
        new("Beto Silva",      "Borracha",                    8, 1.20m,  new DateTime(2026, 5, 4)),
        new("Carla Andrade",   "Lápis HB",                   12, 0.90m,  new DateTime(2026, 5, 6)),
        new("Carla Andrade",   "Apontador",                   2, 3.40m,  new DateTime(2026, 5, 7)),
        new("Carla Andrade",   "Régua 30cm",                  4, 5.50m,  new DateTime(2026, 5, 8)),
        new("Daniel Pereira",  "Mochila Escolar",             1, 148.90m, new DateTime(2026, 5, 9)),
        new("Daniel Pereira",  "Estojo de Lápis",             1, 39.00m, new DateTime(2026, 5, 10)),
    ];

    public static IReadOnlyList<Produto> Produtos() =>
    [
        new("0001", "Caneta Bic Azul",             "7891234567890", 3.20m,  2.50m),
        new("0002", "Caneta Bic Vermelha",         "7891234567891", 3.20m,  2.50m),
        new("0003", "Caderno Brochura 96 folhas",  "7891234567892", 34.90m, 27.40m),
        new("0004", "Marcador de Texto Amarelo",   "7891234567893", 14.90m, 11.90m),
        new("0005", "Borracha Branca",             "7891234567894", 1.50m,  1.20m),
        new("0006", "Lápis HB Faber-Castell",      "7891234567895", 1.20m,  0.90m),
        new("0007", "Apontador com Depósito",      "7891234567896", 4.20m,  3.40m),
        new("0008", "Régua 30cm Acrílica",         "7891234567897", 6.90m,  5.50m),
        new("0009", "Mochila Escolar Reforçada",   "7891234567898", 189.00m, 148.90m),
        new("0010", "Estojo de Lápis Premium",     "7891234567899", 49.00m,  39.00m),
    ];

    public static IReadOnlyList<CaixaMovimento> Caixa()
    {
        // Banded reports require the dataset to be pre-sorted by the grouping key.
        var raw = new[]
        {
            new CaixaMovimento(new DateTime(2026, 5, 23,  9, 12, 0), "NFC-e 001234", "Dinheiro",         45.20m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 10,  3, 0), "NFC-e 001235", "Cartão Débito",   128.50m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 11, 47, 0), "NFC-e 001236", "PIX",              67.80m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 12, 21, 0), "NFC-e 001237", "Dinheiro",         18.90m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 13, 15, 0), "NFC-e 001238", "Cartão Crédito",  234.00m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 14, 42, 0), "NFC-e 001239", "PIX",              89.30m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 15, 30, 0), "NFC-e 001240", "Cartão Débito",    55.40m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 16, 11, 0), "NFC-e 001241", "Dinheiro",         12.50m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 17,  3, 0), "NFC-e 001242", "PIX",             145.00m),
            new CaixaMovimento(new DateTime(2026, 5, 23, 18, 25, 0), "NFC-e 001243", "Cartão Crédito",  312.70m),
        };
        return raw.OrderBy(r => r.FormaPagamento).ThenBy(r => r.Data).ToList();
    }
}
