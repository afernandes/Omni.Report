namespace Reporting.Samples.CodeFirst.Data;

/// <summary>Single item line on a NFC-e (Nota Fiscal de Consumidor Eletrônica) receipt.</summary>
public sealed record NfceItem(
    int Numero,
    string Codigo,
    string Descricao,
    decimal Quantidade,
    string Unidade,
    decimal ValorUnitario)
{
    public decimal ValorTotal => Quantidade * ValorUnitario;
}

public static class NfceSampleData
{
    public static IReadOnlyList<NfceItem> Itens() =>
    [
        new(1, "0001", "Caneta Bic Azul",          2,    "UN",   2.50m),
        new(2, "0003", "Caderno Brochura 96fls",   1,    "UN",  27.40m),
        new(3, "0006", "Lápis HB Faber-Castell",   3,    "UN",   0.90m),
        new(4, "0004", "Marcador de Texto Amarelo", 1,   "UN",  11.90m),
    ];

    public const string ChaveAcesso =
        "35260512345678000190650010000123451000000017";

    public const string CnpjEmitente = "12.345.678/0001-90";
    public const string NomeEmitente = "PAPELARIA EXEMPLO LTDA";
    public const string EnderecoEmitente = "Av. Brasil, 1.000 — Centro — São Paulo/SP";

    public const string ProtocoloAutorizacao = "135260000000123 — 23/05/2026 14:32";
}
