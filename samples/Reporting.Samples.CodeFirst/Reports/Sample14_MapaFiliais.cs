using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Map element plotting each branch by latitude/longitude over a real vector basemap: the
/// bundled simplified Brazil outline (shape set "brazil" from Reporting.Maps) plus a Web-Mercator
/// graticule. A detail list of branches and a revenue total follow below.
/// </summary>
/// <remarks>The host must call <c>Reporting.Maps.MapShapes.RegisterBuiltIns()</c> once so the
/// "brazil" shape set resolves — see Program.cs.</remarks>
public static class Sample14_MapaFiliais
{
    public static Report Build(IEnumerable<Filial>? rows = null) =>
        ReportBuilder
            .Create("Mapa de Filiais")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Filiais", rows ?? DashboardData.Filiais())
            .ReportHeader(h => h.Height(120)
                .Text("Distribuição de Filiais")
                    .At(0, 0).Size(174, 10).Font("Arial", 15, FontStyle.Bold).Center()
                .Text("Map — projeção Web Mercator, contorno do Brasil (shape) + graticule")
                    .At(0, 11).Size(174, 6).Center().Color(Color.Gray)
                .Map("Fields.Lat", "Fields.Lon")
                    .ShapeSet("brazil")   // contorno simplificado do pacote Reporting.Maps
                    .Graticule()
                    .At(0, 18).Size(174, 100))
            .PageHeader(h => h.Height(7)
                .Label("Filial").At(0, 0).Size(92, 6).Bold()
                .Label("UF").At(92, 0).Size(18, 6).Bold()
                .Label("Faturamento").At(112, 0).Size(62, 6).Bold().AlignRight()
                .Line().From(0, 6).To(174, 6).Thickness(0.25))
            .Detail(d => d.Height(6)
                .Text("{Fields.Nome}").At(0, 0).Size(92, 6)
                .Text("{Fields.Uf}").At(92, 0).Size(18, 6)
                .Text("{Fields.Faturamento:C}").At(112, 0).Size(62, 6).AlignRight())
            .ReportFooter(f => f.Height(10)
                .Line().From(0, 0).To(174, 0).Thickness(0.5)
                .Text("Faturamento total: {Sum(Fields.Faturamento):C}")
                    .At(0, 2).Size(174, 7).Font("Arial", 11, FontStyle.Bold).AlignRight())
            .Build();
}
