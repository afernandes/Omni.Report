namespace Reporting.Samples.CodeFirst.Data;

/// <summary>Monthly sales with a target — drives the charts, sparkline, gauge and data bars in
/// Sample 12 (visual dashboard).</summary>
public sealed record VendaMensal(string Mes, decimal Total, decimal Meta);

/// <summary>A store branch with geo-coordinates — plotted by the Map element in Sample 14.</summary>
public sealed record Filial(string Nome, string Uf, double Lat, double Lon, decimal Faturamento);

/// <summary>A line item whose tax/net are computed by a custom C# Code block in Sample 15.</summary>
public sealed record ItemTributo(string Produto, decimal Valor);

public static class DashboardData
{
    public static IReadOnlyList<VendaMensal> Mensal() =>
    [
        new("Jan", 32000m, 30000m),
        new("Fev", 41000m, 35000m),
        new("Mar", 38500m, 36000m),
        new("Abr", 45200m, 40000m),
        new("Mai", 51000m, 45000m),
        new("Jun", 47800m, 48000m),
    ];

    public static IReadOnlyList<Filial> Filiais() =>
    [
        new("São Paulo",      "SP", -23.55, -46.63, 128500m),
        new("Rio de Janeiro", "RJ", -22.91, -43.20,  98700m),
        new("Belo Horizonte", "MG", -19.92, -43.94,  61200m),
        new("Curitiba",       "PR", -25.43, -49.27,  54800m),
        new("Porto Alegre",   "RS", -30.03, -51.23,  47300m),
        new("Salvador",       "BA", -12.97, -38.50,  52900m),
        new("Recife",         "PE",  -8.05, -34.88,  44100m),
        new("Brasília",       "DF", -15.79, -47.88,  73600m),
        new("Fortaleza",      "CE",  -3.73, -38.52,  39800m),
        new("Manaus",         "AM",  -3.10, -60.02,  28500m),
    ];

    public static IReadOnlyList<ItemTributo> ItensTributo() =>
    [
        new("Notebook",          3500.00m),
        new("Monitor 27\"",      1280.00m),
        new("Teclado mecânico",   420.00m),
        new("Mouse sem fio",      150.00m),
        new("Headset",            380.00m),
    ];
}
