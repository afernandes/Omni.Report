using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.ViewModels;

// Toolbox presentation lives next to the value via [ToolboxElement]; the ElementToolbox reflects over
// these (ToolboxCatalog) so a new annotated kind appears in the palette with no markup edits.
public enum DesignerElementKind
{
    [ToolboxElement("Básicos", "Label", "hash", "Texto literal", Hotkey = "L", Draggable = true, DefaultText = "Texto")]
    Label,
    [ToolboxElement("Básicos", "TextBox", "type", "Expressão / template", Hotkey = "T", Draggable = true, DefaultExpression = "{Fields.X}")]
    TextBox,
    [ToolboxElement("Básicos", "Line", "minus", "Linha", Draggable = true, DefaultWidthMm = 50, DefaultHeightMm = 0)]
    Line,
    [ToolboxElement("Básicos", "Rectangle", "square", "Retângulo", Draggable = true)]
    Rectangle,
    [ToolboxElement("Básicos", "Ellipse", "circle", "Elipse", Draggable = true)]
    Ellipse,
    [ToolboxElement("Básicos", "Picture", "image", "Imagem", Draggable = true)]
    Image,
    [ToolboxElement("Básicos", "Barcode", "barcode", "Código de barras", Draggable = true, DefaultWidthMm = 50, DefaultHeightMm = 15, DefaultExpression = "1234567890")]
    Barcode,
    [ToolboxElement("Básicos", "QR Code", "qr-code", "QR Code", Draggable = true, DefaultWidthMm = 25, DefaultHeightMm = 25, DefaultExpression = "https://example.com")]
    QrCode,
    [ToolboxElement("Gráficos", "Chart", "bar-chart-2", "Gráfico (barras/linhas/pizza)", DefaultWidthMm = 80, DefaultHeightMm = 55)]
    Chart,
    [ToolboxElement("Dados", "Table", "table", "Tabela bandada", DefaultWidthMm = 120, DefaultHeightMm = 30, PreviewLabel = "▦ Tabela")]
    Tablix,
    [ToolboxElement("Gráficos", "Gauge", "gauge", "Medidor", DefaultWidthMm = 50, DefaultHeightMm = 40, PreviewLabel = "◷ Medidor")]
    Gauge,
    [ToolboxElement("Avançados", "Data Bar", "bar-chart-3", "Barra de dados proporcional", PreviewLabel = "▬ Barra de dados")]
    DataBar,
    [ToolboxElement("Gráficos", "Sparkline", "trending-up", "Mini-gráfico", DefaultWidthMm = 40, DefaultHeightMm = 15, PreviewLabel = "∿ Mini-gráfico")]
    Sparkline,
    [ToolboxElement("Avançados", "Indicator", "locate", "Indicador KPI (seta / forma / rating)", DefaultWidthMm = 14, DefaultHeightMm = 14, PreviewLabel = "◆ Indicador")]
    Indicator,
    [ToolboxElement("Gráficos", "Map", "map", "Mapa", DefaultWidthMm = 80, DefaultHeightMm = 60, PreviewLabel = "🗺 Mapa")]
    Map,
    [ToolboxElement("Avançados", "Subreport", "files", "Sub-relatório embutido (referência a outro report)", PreviewLabel = "📄 Sub-relatório")]
    Subreport,
    [ToolboxElement("Avançados", "Code", "code", "Bloco de código C#/VB (Code.Metodo) — via Roslyn opt-in", PreviewLabel = "{ } Código")]
    Code,
}
