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

/// <summary>A single conditional formatting rule on an element. Mutable wrapper around
/// <see cref="ConditionalFormat"/>.</summary>
public sealed class ConditionalFormatRule : Notifying
{
    private string _condition = "Fields.Total > 1000";
    public string Condition { get => _condition; set => Set(ref _condition, value); }

    private Color? _foreColor;
    public Color? ForeColor { get => _foreColor; set => Set(ref _foreColor, value); }

    private Color? _backColor;
    public Color? BackColor { get => _backColor; set => Set(ref _backColor, value); }

    private bool _bold;
    public bool Bold { get => _bold; set => Set(ref _bold, value); }

    private bool _italic;
    public bool Italic { get => _italic; set => Set(ref _italic, value); }

    internal ConditionalFormat ToConditionalFormat()
    {
        var fontStyle = FontStyle.Regular;
        if (Bold) fontStyle |= FontStyle.Bold;
        if (Italic) fontStyle |= FontStyle.Italic;
        var style = new Style(
            Font: fontStyle == FontStyle.Regular ? null : new Font("Arial", 10, fontStyle),
            ForeColor: ForeColor,
            BackColor: BackColor);
        return new ConditionalFormat(Condition, style);
    }

    internal static ConditionalFormatRule From(ConditionalFormat cf)
    {
        var rule = new ConditionalFormatRule
        {
            Condition = cf.Condition,
            ForeColor = cf.Style.ForeColor,
            BackColor = cf.Style.BackColor,
        };
        if (cf.Style.Font is { } f)
        {
            rule.Bold = (f.Style & FontStyle.Bold) != 0;
            rule.Italic = (f.Style & FontStyle.Italic) != 0;
        }
        return rule;
    }
}

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

/// <summary>
/// Mutable wrapper around a <see cref="DrillthroughParameter"/>. Exposed as a row in the
/// Action editor's parameter table: Name + Value expression + Omit toggle.
/// </summary>
public sealed class DrillthroughParameterRule : Notifying
{
    private string _name = string.Empty;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _value = string.Empty;
    /// <summary>Expression evaluated in the SOURCE report's context; the result is passed
    /// to the target report under <see cref="Name"/>.</summary>
    public string Value { get => _value; set => Set(ref _value, value); }

    private bool _omit;
    /// <summary>When true, the parameter is not passed at all — matches RDL <c>&lt;Omit&gt;</c>.</summary>
    public bool Omit { get => _omit; set => Set(ref _omit, value); }

    internal DrillthroughParameter ToCoreParameter() => new(Name, Value, Omit);

    internal static DrillthroughParameterRule From(DrillthroughParameter p) =>
        new() { Name = p.Name, Value = p.Value, Omit = p.Omit };
}

/// <summary>Mutable wrapper around a chart <see cref="ChartSeries"/> — one row in the chart
/// editor: name + category/value expressions + optional colour.</summary>
public sealed class ChartSeriesRule : Notifying
{
    private string _name = "Série 1";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _categoryExpression = "Fields.Categoria";
    public string CategoryExpression { get => _categoryExpression; set => Set(ref _categoryExpression, value); }

    private string _valueExpression = "Fields.Valor";
    public string ValueExpression { get => _valueExpression; set => Set(ref _valueExpression, value); }

    private Color? _color;
    public Color? Color { get => _color; set => Set(ref _color, value); }

    /// <summary>Series colour as a <c>#RRGGBB</c> hex string, for the PropertyGrid colour picker.
    /// Null model colour surfaces a sensible default; an unparseable value is ignored.</summary>
    public string ColorHex
    {
        get => _color?.ToHex() ?? "#4F46E5";
        set { try { Color = Reporting.Styling.Color.FromHex(value); } catch (FormatException) { } }
    }

    private string _sizeExpression = string.Empty;
    /// <summary>Bubble marker size expression — used when the chart kind is <c>Bubble</c>.</summary>
    public string SizeExpression { get => _sizeExpression; set => Set(ref _sizeExpression, value); }

    private string _highExpression = string.Empty;
    /// <summary>Stock high expression — used when the chart kind is <c>Stock</c>.</summary>
    public string HighExpression { get => _highExpression; set => Set(ref _highExpression, value); }

    private string _lowExpression = string.Empty;
    /// <summary>Stock low expression — used when the chart kind is <c>Stock</c>.</summary>
    public string LowExpression { get => _lowExpression; set => Set(ref _lowExpression, value); }

    internal ChartSeries ToSeries() => new(Name, CategoryExpression, ValueExpression, Color,
        SizeExpression: string.IsNullOrWhiteSpace(SizeExpression) ? null : SizeExpression,
        HighExpression: string.IsNullOrWhiteSpace(HighExpression) ? null : HighExpression,
        LowExpression: string.IsNullOrWhiteSpace(LowExpression) ? null : LowExpression);

    internal static ChartSeriesRule From(ChartSeries s) => new()
    {
        Name = s.Name,
        CategoryExpression = s.CategoryExpression,
        ValueExpression = s.ValueExpression,
        Color = s.Color,
        SizeExpression = s.SizeExpression ?? string.Empty,
        HighExpression = s.HighExpression ?? string.Empty,
        LowExpression = s.LowExpression ?? string.Empty,
    };
}

/// <summary>One Tablix column in the designer editor — a header label + a per-row detail
/// expression + an optional relative width (weight). A width of <c>0</c> means "equal share";
/// when at least one column has a positive weight, the renderer distributes width proportionally.</summary>
public sealed record TablixColumnView(string Header, string Expression, double Width = 0);

/// <summary>
/// Mutable wrapper around a <see cref="ReportElement"/>. The canvas binds to these and
/// the property grid edits them. <see cref="ToElement"/> materializes back to the immutable
/// model when serializing or paginating.
/// </summary>
public sealed class ElementViewModel : Notifying
{
    public ElementViewModel(DesignerElementKind kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    public string Id { get; }
    public DesignerElementKind Kind { get; }

    private static readonly ConcurrentDictionary<DesignerElementKind, bool> TextStyledCache = new();

    /// <summary>True when this element's type renders text — i.e. carries <see cref="TextStyledAttribute"/>
    /// (Label/TextBox/Barcode, and anything derived from them). Drives the "Data" section in the property
    /// grid from METADATA instead of a hard-coded kind list, so a new text-derived element opts in
    /// automatically just by inheriting the attribute. Cached per kind.</summary>
    public bool IsTextStyled => TextStyledCache.GetOrAdd(Kind,
        _ => ToElement().GetType().GetCustomAttribute<TextStyledAttribute>(inherit: true) is not null);

    /// <summary>For advanced elements that don't have a dedicated editor yet (Tablix, Gauge,
    /// DataBar, Sparkline, Indicator, Map), the original domain element loaded from the report.
    /// <see cref="ToElement"/> re-emits it with the current bounds, so the element round-trips
    /// losslessly through the designer and can still be moved/resized.</summary>
    private ReportElement? _sourceElement;

    /// <summary>TextBox <c>&lt;TextRuns&gt;</c> (mixed-style runs) preserved across a load→edit→save in the
    /// designer. The TextBox is a first-class editor (not opaque), so without this mirror an imported report
    /// with multi-run text would silently lose its runs on any edit. There's no run editor yet — this only
    /// guarantees the runs round-trip; authoring them is a follow-up.</summary>
    private Reporting.Common.EquatableArray<TextRun> _textRuns = Reporting.Common.EquatableArray<TextRun>.Empty;
    /// <summary>Children of a container <see cref="DesignerElementKind.Rectangle"/>, materialised as real
    /// child view models (recursively) so the Designer can show, select and edit them — not merely round-trip
    /// them. <see cref="ToElement"/> rebuilds <c>RectangleElement.Children</c> from these. Opaque/advanced
    /// children (Tablix/Subreport/nested Rectangle) survive losslessly because each child VM uses the same
    /// <see cref="FromElement"/>/<see cref="ToElement"/> path (including <see cref="_sourceElement"/> for
    /// opaque kinds), so a load→edit→save cycle never degrades a nested Tablix into a TextBox.</summary>
    public ObservableCollection<ElementViewModel> Children { get; } = new();

    /// <summary>The container Rectangle VM that owns this element when it is a nested child, else null. Lets
    /// the canvas/outline/property-grid treat a child distinctly (coordinates relative to the parent,
    /// delete-from-parent). A back-reference only — never serialised; set when the child is materialised in
    /// <see cref="LoadFrom"/> or cloned.</summary>
    internal ElementViewModel? ParentElement { get; private set; }

    /// <summary>Adds <paramref name="child"/> as a container child: sets the parent back-reference and chains
    /// the child's <c>Changed</c> into this VM's, so editing a nested element bubbles up to the band → report
    /// and the canvas/outline re-render (children don't go through <c>BandViewModel.AddElement</c>, which is
    /// what wires that for top-level elements).</summary>
    internal void AttachChild(ElementViewModel child)
    {
        child.ParentElement = this;
        child.Changed += RaiseChanged;
        Children.Add(child);
    }

    /// <summary>Removes a container child (outline delete), unsubscribing its change bubbling and raising a
    /// change so the canvas drops it.</summary>
    public void RemoveChild(ElementViewModel child)
    {
        if (Children.Remove(child))
        {
            child.Changed -= RaiseChanged;
            child.ParentElement = null;
            RaiseChanged();
        }
    }

    private void DetachAllChildren()
    {
        foreach (var child in Children)
        {
            child.Changed -= RaiseChanged;
        }
        Children.Clear();
    }
    // Style.BackgroundImage: complex record with no dedicated editor yet — preserved verbatim across edit→save
    // so editing an unrelated property (via ToElement) never silently drops an element's background image.
    private Reporting.Styling.BackgroundImage? _backgroundImage;

    /// <summary>True for kinds rendered as an opaque placeholder — their full domain element is
    /// preserved in <see cref="_sourceElement"/> and re-emitted verbatim by <see cref="ToElement"/>.
    /// Tablix/Gauge/DataBar/Sparkline/Indicator/Map have dedicated PropertyGrid editors;
    /// Subreport/Code are preserved round-trip-only (no editor yet) so loading + re-saving a
    /// report that contains them never degrades them into a TextBox.</summary>
    internal static bool IsOpaqueAdvanced(DesignerElementKind kind) => kind
        is DesignerElementKind.Tablix or DesignerElementKind.Gauge or DesignerElementKind.DataBar
        or DesignerElementKind.Sparkline or DesignerElementKind.Indicator or DesignerElementKind.Map
        or DesignerElementKind.Subreport or DesignerElementKind.Code;

    private string? _name;
    public string? Name { get => _name; set => Set(ref _name, value); }

    private string _text = string.Empty;
    public string Text { get => _text; set => Set(ref _text, value); }

    private string _expression = string.Empty;
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private Unit _x = Unit.Zero;
    public Unit X { get => _x; set => Set(ref _x, value); }

    private Unit _y = Unit.Zero;
    public Unit Y { get => _y; set => Set(ref _y, value); }

    private Unit _width = Unit.FromMm(40);
    public Unit Width { get => _width; set => Set(ref _width, value); }

    private Unit _height = Unit.FromMm(6);
    public Unit Height { get => _height; set => Set(ref _height, value); }

    private bool _isBold;
    public bool IsBold { get => _isBold; set => Set(ref _isBold, value); }

    private bool _isItalic;
    public bool IsItalic { get => _isItalic; set => Set(ref _isItalic, value); }

    private bool _isUnderline;
    public bool IsUnderline { get => _isUnderline; set => Set(ref _isUnderline, value); }

    private bool _isStrikethrough;
    public bool IsStrikethrough { get => _isStrikethrough; set => Set(ref _isStrikethrough, value); }

    private string _fontFamily = "Arial";
    public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }

    private double _fontSize = 10;
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

    private Color _foreColor = Color.Black;
    public Color ForeColor { get => _foreColor; set => Set(ref _foreColor, value); }

    private Color? _fillColor;
    public Color? FillColor { get => _fillColor; set => Set(ref _fillColor, value); }

    private Color? _backColor;
    /// <summary>Background colour of the text/data element (<see cref="Style.BackColor"/>).</summary>
    public Color? BackColor { get => _backColor; set => Set(ref _backColor, value); }

    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
    public HorizontalAlignment HorizontalAlignment { get => _horizontalAlignment; set => Set(ref _horizontalAlignment, value); }

    private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
    public VerticalAlignment VerticalAlignment { get => _verticalAlignment; set => Set(ref _verticalAlignment, value); }

    private bool _wordWrap = true;
    public bool WordWrap { get => _wordWrap; set => Set(ref _wordWrap, value); }

    private Thickness? _padding;
    public Thickness? Padding { get => _padding; set => Set(ref _padding, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    private string? _visibleExpr;
    /// <summary>RDL <c>&lt;Visibility&gt;</c> expression — when non-empty the element renders only when
    /// it evaluates to true (layered on top of <see cref="IsVisible"/>). Base-element parameter.</summary>
    public string? VisibleExpr { get => _visibleExpr; set => Set(ref _visibleExpr, value); }

    private double _cornerRadiusMm;
    /// <summary>Rounded-corner radius in millimetres for a <see cref="DesignerElementKind.Rectangle"/>.</summary>
    public double CornerRadiusMm { get => _cornerRadiusMm; set => Set(ref _cornerRadiusMm, value); }

    private bool _canGrow;
    public bool CanGrow { get => _canGrow; set => Set(ref _canGrow, value); }

    private bool _canShrink;
    public bool CanShrink { get => _canShrink; set => Set(ref _canShrink, value); }

    private bool _keepTogether = true;
    public bool KeepTogether { get => _keepTogether; set => Set(ref _keepTogether, value); }

    private bool _isLocked;
    /// <summary>When true, drag/resize gestures are ignored by the JS interop. Useful to
    /// freeze decorative chrome (logos, watermarks, footer notes) so the data layout below
    /// can be edited without bumping them.</summary>
    public bool IsLocked { get => _isLocked; set => Set(ref _isLocked, value); }

    private string? _format;
    /// <summary>Default format string for the rendered value (e.g. <c>"C"</c>, <c>"N2"</c>,
    /// <c>"dd/MM/yyyy"</c>). Persisted into <see cref="Style.Format"/>.</summary>
    public string? Format { get => _format; set => Set(ref _format, value); }

    private Border? _border;
    /// <summary>Box border (uniform; per-side borders go through <see cref="Border"/>).</summary>
    public Border? Border { get => _border; set => Set(ref _border, value); }

    private byte[]? _inlineImageData;
    /// <summary>Inline image bytes when <see cref="Kind"/> is <see cref="DesignerElementKind.Image"/>.
    /// Persisted in <see cref="ImageElement.InlineData"/>.</summary>
    public byte[]? InlineImageData { get => _inlineImageData; set => Set(ref _inlineImageData, value); }

    // ── RDL Phase 1 extensions ──────────────────────────────────────────────────

    private string? _bookmark;
    /// <summary>RDL <c>&lt;Bookmark&gt;</c>: navigation anchor used by other elements'
    /// <see cref="Action"/> with <see cref="ActionKind.BookmarkLink"/>. Round-trips through
    /// .repx. Empty / whitespace strings are treated as "no bookmark" by the renderer.</summary>
    public string? Bookmark { get => _bookmark; set => Set(ref _bookmark, value); }

    private string? _documentMapLabel;
    /// <summary>RDL <c>&lt;Label&gt;</c> (DocumentMap label). When non-null, the element
    /// contributes an entry to the document map / outline panel shown by interactive viewers.</summary>
    public string? DocumentMapLabel { get => _documentMapLabel; set => Set(ref _documentMapLabel, value); }

    private string? _toggleItemId;
    /// <summary>RDL <c>&lt;ToggleItem&gt;</c>: bookmark of the element that toggles THIS
    /// element's visibility (drill-down). When set, the linked element renders an expand
    /// chevron in interactive viewers.</summary>
    public string? ToggleItemId { get => _toggleItemId; set => Set(ref _toggleItemId, value); }

    private bool _initiallyHidden;
    /// <summary>RDL <c>&lt;Hidden&gt;</c>: when paired with <see cref="ToggleItemId"/>, the
    /// element starts collapsed; the user expands it via the toggle chevron.</summary>
    public bool InitiallyHidden { get => _initiallyHidden; set => Set(ref _initiallyHidden, value); }

    private ActionKind _actionKind = ActionKind.Hyperlink;
    /// <summary>Discriminator selecting which action variant is active. Combined with
    /// <see cref="HasAction"/> to decide whether to emit an action child at serialize time.</summary>
    public ActionKind ActionKind { get => _actionKind; set => Set(ref _actionKind, value); }

    private bool _hasAction;
    /// <summary>Master toggle: when false, the element has no action regardless of the
    /// other action-related fields. Drives the PropertyGrid checkbox / icon.</summary>
    public bool HasAction { get => _hasAction; set => Set(ref _hasAction, value); }

    private string? _hyperlink;
    /// <summary>URL or expression-producing-URL when <see cref="ActionKind"/> is
    /// <see cref="ActionKind.Hyperlink"/>.</summary>
    public string? Hyperlink { get => _hyperlink; set => Set(ref _hyperlink, value); }

    private string? _bookmarkLinkId;
    /// <summary>Target bookmark id when <see cref="ActionKind"/> is
    /// <see cref="ActionKind.BookmarkLink"/>.</summary>
    public string? BookmarkLinkId { get => _bookmarkLinkId; set => Set(ref _bookmarkLinkId, value); }

    private string? _drillthroughReportName;
    /// <summary>Target report name when <see cref="ActionKind"/> is
    /// <see cref="ActionKind.DrillthroughReport"/>. Resolution is host-mediated — the
    /// viewer raises an event with this name; the host opens the matching .repx.</summary>
    public string? DrillthroughReportName { get => _drillthroughReportName; set => Set(ref _drillthroughReportName, value); }

    /// <summary>Drillthrough parameters — order-preserved. Mutated through the
    /// PropertyGrid Action editor; round-trips through .repx.</summary>
    public ObservableCollection<DrillthroughParameterRule> DrillthroughParameters { get; } = new();

    private BarcodeSymbology _symbology = BarcodeSymbology.Code128;
    /// <summary>Barcode symbology (Code128, EAN-13, ITF, …). Applies when
    /// <see cref="Kind"/> is <see cref="DesignerElementKind.Barcode"/>. When the kind is
    /// <see cref="DesignerElementKind.QrCode"/> this field is forced to
    /// <see cref="BarcodeSymbology.QrCode"/> at <see cref="ToElement"/> time, so
    /// editing it on a QR element has no effect (the PropertyGrid hides the picker).</summary>
    public BarcodeSymbology Symbology { get => _symbology; set => Set(ref _symbology, value); }

    private QrEccLevel _qrEcc = QrEccLevel.Medium;
    /// <summary>QR error-correction level. Higher = bigger symbol, more damage tolerance.
    /// Only meaningful when <see cref="Kind"/> is <see cref="DesignerElementKind.QrCode"/>.</summary>
    public QrEccLevel QrEcc { get => _qrEcc; set => Set(ref _qrEcc, value); }

    private bool _barcodeShowText = true;
    /// <summary>Whether a 1D barcode prints the human-readable text strip below the bars.
    /// Ignored for QR (which never has one). Barcode kind only.</summary>
    public bool BarcodeShowText { get => _barcodeShowText; set => Set(ref _barcodeShowText, value); }

    // ── Line (DesignerElementKind.Line) ─────────────────────────────────────────

    private LineDirection _lineDir = LineDirection.Horizontal;
    /// <summary>Line orientation (horizontal / vertical / diagonal). Applies when
    /// <see cref="Kind"/> is <see cref="DesignerElementKind.Line"/>.</summary>
    public LineDirection LineDir { get => _lineDir; set => Set(ref _lineDir, value); }

    // ── Image (DesignerElementKind.Image) ───────────────────────────────────────

    private ImageSizing _imageSizing = ImageSizing.Fit;
    /// <summary>How the image fits its bounds (Fit / Fill / Stretch / Native). Image kind only.</summary>
    public ImageSizing ImageSizing { get => _imageSizing; set => Set(ref _imageSizing, value); }

    private string? _imagePath;
    /// <summary>File path or URL of the image when it is not embedded inline. Image kind only.</summary>
    public string? ImagePath { get => _imagePath; set => Set(ref _imagePath, value); }

    private string? _imageExpression;
    /// <summary>Expression that yields the image path/bytes per row (data-bound image). Image kind only.</summary>
    public string? ImageExpression { get => _imageExpression; set => Set(ref _imageExpression, value); }

    // ── Chart (DesignerElementKind.Chart) ───────────────────────────────────────

    private ChartKind _chartKind = ChartKind.Bar;
    /// <summary>Chart type (bar/line/pie). Applies when <see cref="Kind"/> is
    /// <see cref="DesignerElementKind.Chart"/>.</summary>
    public ChartKind ChartKind { get => _chartKind; set => Set(ref _chartKind, value); }

    private string _chartTitle = string.Empty;
    public string ChartTitle { get => _chartTitle; set => Set(ref _chartTitle, value); }

    private bool _showLegend = true;
    public bool ShowLegend { get => _showLegend; set => Set(ref _showLegend, value); }

    /// <summary>Chart series. Each row contributes category/value points; round-trips through
    /// <see cref="ChartElement.Series"/>.</summary>
    public ObservableCollection<ChartSeriesRule> ChartSeries { get; } = new();

    // ── Opaque-advanced editors ─────────────────────────────────────────────────
    // DataBar / Sparkline / Map are edited directly on the preserved domain element
    // (_sourceElement); the opaque ToElement path re-emits it with the current bounds. This keeps
    // a single source of truth and avoids duplicating every field on the view model.

    private T? Src<T>() where T : ReportElement => _sourceElement as T;

    private void Mutate<T>(Func<T, T> mutate) where T : ReportElement, new()
    {
        _sourceElement = mutate(_sourceElement as T ?? new T());
        RaiseChanged();
    }

    public string DataBarValue { get => Src<DataBarElement>()?.ValueExpression ?? "0"; set => Mutate<DataBarElement>(e => e with { ValueExpression = value }); }
    public string DataBarMin { get => Src<DataBarElement>()?.MinimumExpression ?? "0"; set => Mutate<DataBarElement>(e => e with { MinimumExpression = value }); }
    public string DataBarMax { get => Src<DataBarElement>()?.MaximumExpression ?? "100"; set => Mutate<DataBarElement>(e => e with { MaximumExpression = value }); }
    public string DataBarFill { get => Src<DataBarElement>()?.FillColor ?? "#C2410C"; set => Mutate<DataBarElement>(e => e with { FillColor = value }); }

    public SparklineKind SparkKind { get => Src<SparklineElement>()?.Kind ?? SparklineKind.Line; set => Mutate<SparklineElement>(e => e with { Kind = value }); }
    public string SparkValue { get => Src<SparklineElement>()?.ValueExpression ?? "Fields.Value"; set => Mutate<SparklineElement>(e => e with { ValueExpression = value }); }
    public string SparkCategory { get => Src<SparklineElement>()?.CategoryExpression ?? string.Empty; set => Mutate<SparklineElement>(e => e with { CategoryExpression = string.IsNullOrWhiteSpace(value) ? null : value }); }
    public string SparkDataSet { get => Src<SparklineElement>()?.DataSetName ?? string.Empty; set => Mutate<SparklineElement>(e => e with { DataSetName = string.IsNullOrWhiteSpace(value) ? null : value }); }

    public string MapLatitude { get => Src<MapElement>()?.LatitudeExpression ?? string.Empty; set => Mutate<MapElement>(e => e with { LatitudeExpression = string.IsNullOrWhiteSpace(value) ? null : value }); }
    public string MapLongitude { get => Src<MapElement>()?.LongitudeExpression ?? string.Empty; set => Mutate<MapElement>(e => e with { LongitudeExpression = string.IsNullOrWhiteSpace(value) ? null : value }); }
    public string MapDataSet { get => Src<MapElement>()?.DataSetName ?? string.Empty; set => Mutate<MapElement>(e => e with { DataSetName = string.IsNullOrWhiteSpace(value) ? null : value }); }
    /// <summary>Draws the lat/long graticule behind the data (maps to <see cref="MapElement.ShowGraticule"/>).</summary>
    public bool MapGraticule { get => Src<MapElement>()?.ShowGraticule ?? false; set => Mutate<MapElement>(e => e with { ShowGraticule = value }); }
    /// <summary>Named built-in shape set, e.g. "brazil"/"south-america" (<see cref="MapElement.ShapeSet"/>).</summary>
    public string MapShapeSet { get => Src<MapElement>()?.ShapeSet ?? string.Empty; set => Mutate<MapElement>(e => e with { ShapeSet = string.IsNullOrWhiteSpace(value) ? null : value }); }
    /// <summary>Inline GeoJSON vector basemap (<see cref="MapElement.ShapesGeoJson"/>); takes precedence over the shape set.</summary>
    public string MapShapesGeoJson { get => Src<MapElement>()?.ShapesGeoJson ?? string.Empty; set => Mutate<MapElement>(e => e with { ShapesGeoJson = string.IsNullOrWhiteSpace(value) ? null : value }); }
    /// <summary>Polygon fill colour for shapes (<see cref="MapElement.ShapeFill"/>).</summary>
    public string MapShapeFill { get => Src<MapElement>()?.ShapeFill ?? "#E8EDE4"; set => Mutate<MapElement>(e => e with { ShapeFill = string.IsNullOrWhiteSpace(value) ? "#E8EDE4" : value }); }
    /// <summary>Shape outline / graticule stroke colour (<see cref="MapElement.ShapeStroke"/>).</summary>
    public string MapShapeStroke { get => Src<MapElement>()?.ShapeStroke ?? "#9CA3AF"; set => Mutate<MapElement>(e => e with { ShapeStroke = string.IsNullOrWhiteSpace(value) ? "#9CA3AF" : value }); }
    /// <summary>Tile/basemap provider name (<see cref="MapElement.Basemap"/>) — consumed by the online tile layer.</summary>
    public string MapBasemap { get => Src<MapElement>()?.Basemap ?? string.Empty; set => Mutate<MapElement>(e => e with { Basemap = string.IsNullOrWhiteSpace(value) ? null : value }); }

    // Code (custom C#/VB block; evaluated via the opt-in Reporting.Expressions.Roslyn package)
    /// <summary>Source text of the code block — helper methods reachable as <c>Code.Method(...)</c>
    /// (<see cref="CodeElement.Source"/>).</summary>
    public string CodeSource { get => Src<CodeElement>()?.Source ?? string.Empty; set => Mutate<CodeElement>(e => e with { Source = value }); }
    /// <summary>Source language of the code block (<see cref="CodeElement.Language"/>).</summary>
    public CodeLanguage CodeLang { get => Src<CodeElement>()?.Language ?? CodeLanguage.CSharp; set => Mutate<CodeElement>(e => e with { Language = value }); }

    // Subreport (embeds a child report at the element's bounds)
    /// <summary>Registry id of the child report (<see cref="SubreportElement.ReportId"/>).</summary>
    public string SubreportReportId { get => Src<SubreportElement>()?.ReportId ?? string.Empty; set => Mutate<SubreportElement>(e => e with { ReportId = string.IsNullOrWhiteSpace(value) ? null : value }); }
    /// <summary>Parent-context expression yielding the child's data (<see cref="SubreportElement.DataExpression"/>).</summary>
    public string SubreportDataExpression { get => Src<SubreportElement>()?.DataExpression ?? string.Empty; set => Mutate<SubreportElement>(e => e with { DataExpression = string.IsNullOrWhiteSpace(value) ? null : value }); }
    /// <summary>Parameter bindings edited as one <c>name=expression</c> per line. Maps to
    /// <see cref="SubreportElement.ParameterBindings"/>; expressions are evaluated in the parent context.</summary>
    public string SubreportParametersText
    {
        get
        {
            var src = Src<SubreportElement>();
            return src is null ? string.Empty : string.Join("\n", src.ParameterBindings.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        set => Mutate<SubreportElement>(e => e with { ParameterBindings = ParseBindings(value) });
    }

    private static Reporting.Common.EquatableDictionary<string, string> ParseBindings(string? text)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var trimmed = line.Trim();
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue; // skip blank lines and lines without a key
            var key = trimmed[..eq].Trim();
            if (key.Length == 0) continue;
            pairs.Add(new KeyValuePair<string, string>(key, trimmed[(eq + 1)..].Trim()));
        }
        // Last binding for a repeated key wins — ImmutableDictionary would otherwise throw on dupes.
        return new Reporting.Common.EquatableDictionary<string, string>(
            pairs.GroupBy(p => p.Key).Select(g => g.Last()));
    }

    // Gauge (scalars + coloured ranges)
    public GaugeKind GaugeType { get => Src<GaugeElement>()?.Kind ?? GaugeKind.Radial; set => Mutate<GaugeElement>(e => e with { Kind = value }); }
    public string GaugeValue { get => Src<GaugeElement>()?.ValueExpression ?? "0"; set => Mutate<GaugeElement>(e => e with { ValueExpression = value }); }
    public string GaugeMin { get => Src<GaugeElement>()?.MinimumExpression ?? "0"; set => Mutate<GaugeElement>(e => e with { MinimumExpression = value }); }
    public string GaugeMax { get => Src<GaugeElement>()?.MaximumExpression ?? "100"; set => Mutate<GaugeElement>(e => e with { MaximumExpression = value }); }
    public IReadOnlyList<GaugeRange> GaugeRanges => Src<GaugeElement>()?.Ranges.ToList() ?? [];
    public void AddGaugeRange() => Mutate<GaugeElement>(e => e with { Ranges = Append(e.Ranges, new GaugeRange("0", "50", "#16A34A")) });
    public void RemoveGaugeRange(int index) => Mutate<GaugeElement>(e => e with { Ranges = RemoveAt(e.Ranges, index) });
    public void SetGaugeRange(int index, string? start = null, string? end = null, string? color = null)
        => Mutate<GaugeElement>(e => e with { Ranges = ReplaceAt(e.Ranges, index, r => new GaugeRange(start ?? r.StartExpression, end ?? r.EndExpression, color ?? r.ColorHex)) });

    // Indicator (scalars + KPI states)
    public IndicatorKind IndicatorType { get => Src<IndicatorElement>()?.Kind ?? IndicatorKind.DirectionalArrow; set => Mutate<IndicatorElement>(e => e with { Kind = value }); }
    public string IndicatorValue { get => Src<IndicatorElement>()?.ValueExpression ?? "0"; set => Mutate<IndicatorElement>(e => e with { ValueExpression = value }); }
    public IReadOnlyList<IndicatorState> IndicatorStates => Src<IndicatorElement>()?.States.ToList() ?? [];
    public void AddIndicatorState() => Mutate<IndicatorElement>(e => e with { States = Append(e.States, new IndicatorState("0", "50", "")) });
    public void RemoveIndicatorState(int index) => Mutate<IndicatorElement>(e => e with { States = RemoveAt(e.States, index) });
    public void SetIndicatorState(int index, string? start = null, string? end = null, string? icon = null)
        => Mutate<IndicatorElement>(e => e with { States = ReplaceAt(e.States, index, s => new IndicatorState(start ?? s.StartExpression, end ?? s.EndExpression, icon ?? s.IconName)) });

    // Tablix (columns derived from Cells: row 0 = header label, row 1 = detail expression)
    public string TablixDataSet { get => Src<TablixElement>()?.DataSetName ?? string.Empty; set => Mutate<TablixElement>(e => e with { DataSetName = string.IsNullOrWhiteSpace(value) ? null : value }); }

    // ── Tablix matrix / crosstab ─────────────────────────────────────────────────
    /// <summary>True when the Tablix is a matrix (has both a row group and a column group). The
    /// PropertyGrid swaps the flat-column editor for the matrix fields below.</summary>
    public bool TablixIsMatrix => Src<TablixElement>() is { RowGroups.Count: > 0, ColumnGroups.Count: > 0 };

    /// <summary>Row-group expression (left axis of the matrix). Maps to <c>RowGroups[0]</c>.</summary>
    public string TablixRowGroup
    {
        get { var t = Src<TablixElement>(); return t is { RowGroups.Count: > 0 } ? t.RowGroups[0].GroupExpression ?? string.Empty : string.Empty; }
        set => Mutate<TablixElement>(e => e with { RowGroups = SingleGroup("Rows", value) });
    }

    /// <summary>Column-group expression (top axis of the matrix). Maps to <c>ColumnGroups[0]</c>.</summary>
    public string TablixColumnGroup
    {
        get { var t = Src<TablixElement>(); return t is { ColumnGroups.Count: > 0 } ? t.ColumnGroups[0].GroupExpression ?? string.Empty : string.Empty; }
        set => Mutate<TablixElement>(e => e with { ColumnGroups = SingleGroup("Cols", value) });
    }

    /// <summary>All row-group levels (left axis), one expression per line, outer→inner. One line is a
    /// single-level matrix; add lines to <b>nest</b>. Maps to the whole <c>RowGroups</c> array.</summary>
    public string TablixRowGroupsText
    {
        get { var t = Src<TablixElement>(); return t is null ? string.Empty : string.Join("\n", t.RowGroups.Select(g => g.GroupExpression ?? string.Empty)); }
        set => Mutate<TablixElement>(e => e with { RowGroups = GroupsFromText("Rows", value, e.RowGroups) });
    }

    /// <summary>All column-group levels (top axis), one expression per line, outer→inner. Maps to the
    /// whole <c>ColumnGroups</c> array.</summary>
    public string TablixColumnGroupsText
    {
        get { var t = Src<TablixElement>(); return t is null ? string.Empty : string.Join("\n", t.ColumnGroups.Select(g => g.GroupExpression ?? string.Empty)); }
        set => Mutate<TablixElement>(e => e with { ColumnGroups = GroupsFromText("Cols", value, e.ColumnGroups) });
    }

    /// <summary>SSRS-style group totals: a subtotal row after each outer row-group block plus a grand total
    /// row at the bottom. Maps to <c>TablixElement.RowSubtotals</c>.</summary>
    public bool TablixRowSubtotals
    {
        get => Src<TablixElement>()?.RowSubtotals ?? false;
        set => Mutate<TablixElement>(e => e with { RowSubtotals = value });
    }

    /// <summary>Column-axis group totals: a subtotal column after each outer column-group block plus a grand
    /// total column at the right. Maps to <c>TablixElement.ColumnSubtotals</c>.</summary>
    public bool TablixColumnSubtotals
    {
        get => Src<TablixElement>()?.ColumnSubtotals ?? false;
        set => Mutate<TablixElement>(e => e with { ColumnSubtotals = value });
    }

    /// <summary>Subtotal label template — <c>{0}</c> is the group value (e.g. "Total {0}"). Empty = default.
    /// Maps to <c>TablixElement.SubtotalLabel</c>.</summary>
    public string TablixSubtotalLabel
    {
        get => Src<TablixElement>()?.SubtotalLabel ?? string.Empty;
        set => Mutate<TablixElement>(e => e with { SubtotalLabel = string.IsNullOrWhiteSpace(value) ? null : value });
    }

    /// <summary>Grand-total label. Empty = default ("Total geral"). Maps to
    /// <c>TablixElement.GrandTotalLabel</c>.</summary>
    public string TablixGrandTotalLabel
    {
        get => Src<TablixElement>()?.GrandTotalLabel ?? string.Empty;
        set => Mutate<TablixElement>(e => e with { GrandTotalLabel = string.IsNullOrWhiteSpace(value) ? null : value });
    }

    /// <summary>Message shown (centred) when the bound dataset has no rows — RDL <c>NoRowsMessage</c>. Accepts
    /// a literal or an expression ("=…"). Empty = nothing rendered. Maps to <c>TablixElement.NoRowsMessage</c>.</summary>
    public string TablixNoRowsMessage
    {
        get => Src<TablixElement>()?.NoRowsMessage ?? string.Empty;
        set => Mutate<TablixElement>(e => e with { NoRowsMessage = string.IsNullOrWhiteSpace(value) ? null : value });
    }

    /// <summary>SortExpression of the primary (outermost) row group — orders the group instances down the
    /// left axis. Empty = data order. Maps to <c>RowGroups[0].SortExpression</c>.</summary>
    public string TablixRowGroupSort
    {
        get { var t = Src<TablixElement>(); return t is { RowGroups.Count: > 0 } ? t.RowGroups[0].SortExpression ?? string.Empty : string.Empty; }
        set => Mutate<TablixElement>(e => e with { RowGroups = WithSort(e.RowGroups, 0, value, null) });
    }

    /// <summary>Whether the primary row group sorts descending.</summary>
    public bool TablixRowGroupSortDescending
    {
        get { var t = Src<TablixElement>(); return t is { RowGroups.Count: > 0 } && t.RowGroups[0].SortDescending; }
        set => Mutate<TablixElement>(e => e with { RowGroups = WithSort(e.RowGroups, 0, null, value) });
    }

    /// <summary>SortExpression of the primary (outermost) column group. Maps to <c>ColumnGroups[0].SortExpression</c>.</summary>
    public string TablixColumnGroupSort
    {
        get { var t = Src<TablixElement>(); return t is { ColumnGroups.Count: > 0 } ? t.ColumnGroups[0].SortExpression ?? string.Empty : string.Empty; }
        set => Mutate<TablixElement>(e => e with { ColumnGroups = WithSort(e.ColumnGroups, 0, value, null) });
    }

    /// <summary>Whether the primary column group sorts descending.</summary>
    public bool TablixColumnGroupSortDescending
    {
        get { var t = Src<TablixElement>(); return t is { ColumnGroups.Count: > 0 } && t.ColumnGroups[0].SortDescending; }
        set => Mutate<TablixElement>(e => e with { ColumnGroups = WithSort(e.ColumnGroups, 0, null, value) });
    }

    /// <summary>Top-left corner label of the matrix (cell 0,0).</summary>
    public string TablixCorner
    {
        get => (TablixCellAt(0, 0) as LabelElement)?.Text ?? string.Empty;
        set => Mutate<TablixElement>(e => e with { Cells = SetTablixCell(e.Cells, 0, 0, new LabelElement { Text = value, Bounds = Rectangle.Empty }) });
    }

    /// <summary>Body value expression — SUMmed per intersection (cell 1,1).</summary>
    public string TablixCellExpr
    {
        get => (TablixCellAt(1, 1) as TextBoxElement)?.Expression ?? string.Empty;
        set => Mutate<TablixElement>(e => e with { Cells = SetTablixCell(e.Cells, 1, 1, new TextBoxElement { Expression = value, Bounds = Rectangle.Empty }) });
    }

    /// <summary>Turns matrix mode on/off. Enabling seeds default groups + corner/body cells so the
    /// fields are editable immediately; disabling clears the groups (and falls back to one flat
    /// column so the table still renders something).</summary>
    public void SetTablixMatrix(bool on)
    {
        if (on)
        {
            Mutate<TablixElement>(e => e with
            {
                RowGroups = e.RowGroups.Count > 0 ? e.RowGroups : SingleGroup("Rows", "Fields.Linha"),
                ColumnGroups = e.ColumnGroups.Count > 0 ? e.ColumnGroups : SingleGroup("Cols", "Fields.Coluna"),
                Cells = SeedMatrixCells(e.Cells),
            });
        }
        else
        {
            Mutate<TablixElement>(e => e with
            {
                RowGroups = Reporting.Common.EquatableArray<TablixGroup>.Empty,
                ColumnGroups = Reporting.Common.EquatableArray<TablixGroup>.Empty,
            });
            if (TablixColumns.Count == 0)
            {
                AddTablixColumn();
            }
        }
    }

    private static Reporting.Common.EquatableArray<TablixGroup> SingleGroup(string name, string expr)
        => string.IsNullOrWhiteSpace(expr)
            ? Reporting.Common.EquatableArray<TablixGroup>.Empty
            : new Reporting.Common.EquatableArray<TablixGroup>([new TablixGroup(name, expr)]);

    /// <summary>Parses a multi-line group editor (one expression per line, blanks skipped) into an
    /// ordered <c>RowGroups</c>/<c>ColumnGroups</c> array — outer level first. Any SortExpression already
    /// set at a level is preserved <b>by line index</b> (editing the expressions in place keeps the sort;
    /// reordering the lines reassigns the sort to whatever now sits at that position).</summary>
    private static Reporting.Common.EquatableArray<TablixGroup> GroupsFromText(
        string prefix, string? text, Reporting.Common.EquatableArray<TablixGroup> existing)
    {
        var levels = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        return levels.Length == 0
            ? Reporting.Common.EquatableArray<TablixGroup>.Empty
            : new Reporting.Common.EquatableArray<TablixGroup>(
                levels.Select((e, i) => new TablixGroup($"{prefix}{i}", e,
                    i < existing.Count ? existing[i].SortExpression : null,
                    i < existing.Count && existing[i].SortDescending)).ToArray());
    }

    /// <summary>Returns the groups with the level at <paramref name="index"/> rebuilt to carry a new
    /// SortExpression and/or descending flag (preserving its GroupExpression). No-op if out of range.</summary>
    private static Reporting.Common.EquatableArray<TablixGroup> WithSort(
        Reporting.Common.EquatableArray<TablixGroup> groups, int index, string? sortExpr, bool? descending)
    {
        if (index < 0 || index >= groups.Count)
        {
            return groups;
        }
        var arr = groups.ToArray();
        var g = arr[index];
        arr[index] = g with
        {
            SortExpression = sortExpr is null ? g.SortExpression : (string.IsNullOrWhiteSpace(sortExpr) ? null : sortExpr),
            SortDescending = descending ?? g.SortDescending,
        };
        return new Reporting.Common.EquatableArray<TablixGroup>(arr);
    }

    private ReportElement? TablixCellAt(int row, int col)
        => Src<TablixElement>()?.Cells.FirstOrDefault(c => c.RowIndex == row && c.ColumnIndex == col)?.Content;

    private static Reporting.Common.EquatableArray<TablixCell> SetTablixCell(
        Reporting.Common.EquatableArray<TablixCell> cells, int row, int col, ReportElement content)
    {
        var list = cells.Where(c => !(c.RowIndex == row && c.ColumnIndex == col)).ToList();
        list.Add(new TablixCell(row, col, content));
        return new Reporting.Common.EquatableArray<TablixCell>(list);
    }

    private static Reporting.Common.EquatableArray<TablixCell> SeedMatrixCells(Reporting.Common.EquatableArray<TablixCell> existing)
    {
        var list = existing.ToList();
        if (!list.Any(c => c.RowIndex == 0 && c.ColumnIndex == 0))
        {
            list.Add(new TablixCell(0, 0, new LabelElement { Text = string.Empty, Bounds = Rectangle.Empty }));
        }
        if (!list.Any(c => c.RowIndex == 1 && c.ColumnIndex == 1 && c.Content is TextBoxElement))
        {
            list.Add(new TablixCell(1, 1, new TextBoxElement { Expression = "Fields.Valor", Bounds = Rectangle.Empty }));
        }
        return new Reporting.Common.EquatableArray<TablixCell>(list);
    }

    public IReadOnlyList<TablixColumnView> TablixColumns
    {
        get
        {
            var t = Src<TablixElement>();
            if (t is null)
            {
                return [];
            }
            int cols = 0;
            foreach (var cell in t.Cells)
            {
                cols = Math.Max(cols, cell.ColumnIndex + 1);
            }
            var headers = new string[cols];
            var exprs = new string[cols];
            foreach (var cell in t.Cells)
            {
                if (cell.ColumnIndex < 0 || cell.ColumnIndex >= cols)
                {
                    continue;
                }
                if (cell.RowIndex == 0 && cell.Content is LabelElement lbl)
                {
                    headers[cell.ColumnIndex] = lbl.Text;
                }
                else if (cell.RowIndex == 1 && cell.Content is TextBoxElement tb)
                {
                    exprs[cell.ColumnIndex] = tb.Expression;
                }
            }
            var widths = t.ColumnWidths;
            var result = new List<TablixColumnView>(cols);
            for (int i = 0; i < cols; i++)
            {
                result.Add(new TablixColumnView(
                    headers[i] ?? string.Empty,
                    exprs[i] ?? string.Empty,
                    i < widths.Count ? widths[i] : 0));
            }
            return result;
        }
    }

    public void AddTablixColumn()
    {
        var list = TablixColumns.ToList();
        list.Add(new TablixColumnView("Coluna", "Fields.X"));
        SetTablixColumns(list);
    }

    public void RemoveTablixColumn(int index)
    {
        var list = TablixColumns.ToList();
        if (index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
        }
        SetTablixColumns(list);
    }

    public void SetTablixColumn(int index, string? header = null, string? expression = null, double? width = null)
    {
        var list = TablixColumns.ToList();
        if (index >= 0 && index < list.Count)
        {
            list[index] = new TablixColumnView(
                header ?? list[index].Header,
                expression ?? list[index].Expression,
                width ?? list[index].Width);
        }
        SetTablixColumns(list);
    }

    private void SetTablixColumns(IReadOnlyList<TablixColumnView> columns)
    {
        var cells = new List<TablixCell>(columns.Count * 2);
        for (int c = 0; c < columns.Count; c++)
        {
            cells.Add(new TablixCell(0, c, new LabelElement { Text = columns[c].Header, Bounds = Rectangle.Empty }));
            cells.Add(new TablixCell(1, c, new TextBoxElement { Expression = columns[c].Expression, Bounds = Rectangle.Empty }));
        }
        // Emit ColumnWidths only when at least one column carries a positive weight — keeps the
        // common equal-width table free of redundant metadata (mirrors the code-first builder).
        var widths = columns.Any(c => c.Width > 0)
            ? new EquatableArray<double>(columns.Select(c => c.Width).ToArray())
            : EquatableArray<double>.Empty;
        Mutate<TablixElement>(e => e with { Cells = new EquatableArray<TablixCell>(cells), ColumnWidths = widths });
    }

    private static EquatableArray<T> Append<T>(EquatableArray<T> arr, T item) where T : IEquatable<T>
    {
        var list = arr.ToList();
        list.Add(item);
        return new EquatableArray<T>(list);
    }

    private static EquatableArray<T> RemoveAt<T>(EquatableArray<T> arr, int index) where T : IEquatable<T>
    {
        var list = arr.ToList();
        if (index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
        }
        return new EquatableArray<T>(list);
    }

    private static EquatableArray<T> ReplaceAt<T>(EquatableArray<T> arr, int index, Func<T, T> replace) where T : IEquatable<T>
    {
        var list = arr.ToList();
        if (index >= 0 && index < list.Count)
        {
            list[index] = replace(list[index]);
        }
        return new EquatableArray<T>(list);
    }

    /// <summary>Conditional formatting rules. Each rule's condition expression is evaluated
    /// at render time; matching rules layer their style overrides onto the element.</summary>
    public ObservableCollection<ConditionalFormatRule> ConditionalFormats { get; } = new();

    /// <summary>Per-property expression bindings (SSRS-style <c>fx</c>): property path → expression,
    /// mirroring <see cref="ReportElement.PropertyExpressions"/>. Tracked here so a load → edit → save
    /// cycle through the designer preserves them (without this the VM would silently drop them), and so
    /// the metadata PropertyGrid can offer an <c>fx</c> toggle per property.</summary>
    private readonly Dictionary<string, string> _propertyExpressions = new(StringComparer.Ordinal);

    /// <summary>Read-only view of the per-property expression bindings keyed by property path.</summary>
    public IReadOnlyDictionary<string, string> PropertyExpressions => _propertyExpressions;

    /// <summary>The expression bound to <paramref name="path"/>, or null when the property uses its
    /// static value.</summary>
    public string? GetPropertyExpression(string path)
        => _propertyExpressions.TryGetValue(path, out var v) ? v : null;

    /// <summary>Binds <paramref name="path"/> to <paramref name="expression"/>, or clears the binding
    /// (reverting to the static value) when the expression is null/blank.</summary>
    public void SetPropertyExpression(string path, string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            _propertyExpressions.Remove(path);
        }
        else
        {
            _propertyExpressions[path] = expression;
        }
        RaiseChanged();
    }

    /// <summary>Reads a metadata descriptor's current value off the materialized element. The generic
    /// editor uses this so it never needs a hand-written getter per property.</summary>
    public object? GetMetaValue(PropertyGridDescriptor descriptor) => descriptor.Get(ToElement());

    /// <summary>Applies a metadata descriptor's immutable setter and re-hydrates this VM from the
    /// result — so ONE generic editor can drive ANY <c>[PropertyGrid]</c> property (basic or advanced)
    /// without per-property plumbing. The static value lands back in the VM; expression bindings and
    /// every other field are preserved (the change round-trips through <see cref="ToElement"/>).</summary>
    public void ApplyMetaSet(PropertyGridDescriptor descriptor, object? value)
    {
        LoadFrom((ReportElement)descriptor.Set(ToElement(), value));
        RaiseChanged();
    }

    public Rectangle Bounds
    {
        get => new(X, Y, Width, Height);
        set
        {
            X = value.X; Y = value.Y; Width = value.Width; Height = value.Height;
        }
    }

    /// <summary>Convert back to the immutable <see cref="ReportElement"/> for serialization
    /// and rendering.</summary>
    public ReportElement ToElement()
    {
        var fontStyle = FontStyle.Regular;
        if (IsBold)          fontStyle |= FontStyle.Bold;
        if (IsItalic)        fontStyle |= FontStyle.Italic;
        if (IsUnderline)     fontStyle |= FontStyle.Underline;
        if (IsStrikethrough) fontStyle |= FontStyle.Strikeout;
        var style = new Style(
            Font: new Font(FontFamily, FontSize, fontStyle),
            ForeColor: ForeColor,
            BackColor: BackColor,
            Border: Border,
            Padding: Padding,
            HorizontalAlignment: HorizontalAlignment,
            VerticalAlignment: VerticalAlignment,
            WordWrap: WordWrap,
            Format: Format,
            BackgroundImage: _backgroundImage);

        ReportElement element = Kind switch
        {
            DesignerElementKind.Label => new LabelElement { Text = Text, Bounds = Bounds },
            DesignerElementKind.TextBox => new TextBoxElement { Expression = Expression, Bounds = Bounds, CanGrow = CanGrow, CanShrink = CanShrink, TextRuns = _textRuns },
            DesignerElementKind.Line => new LineElement { Bounds = Bounds, Direction = LineDir },
            DesignerElementKind.Rectangle => new RectangleElement
            {
                Bounds = Bounds,
                FillColor = FillColor,
                CornerRadius = Unit.FromMm(CornerRadiusMm),
                Children = Children.Count == 0
                    ? Reporting.Common.EquatableArray<ReportElement>.Empty
                    : new Reporting.Common.EquatableArray<ReportElement>(Children.Select(c => c.ToElement()).ToArray()),
            },
            DesignerElementKind.Ellipse => new EllipseElement { Bounds = Bounds, FillColor = FillColor },
            DesignerElementKind.Image => new ImageElement
            {
                Bounds = Bounds,
                // Source kind is inferred from which field the user filled: embedded bytes win,
                // then a per-row expression, otherwise a static path/URL.
                Source = InlineImageData is { Length: > 0 } ? ImageSourceKind.Inline
                    : !string.IsNullOrWhiteSpace(ImageExpression) ? ImageSourceKind.Expression
                    : ImageSourceKind.Path,
                InlineData = InlineImageData is { Length: > 0 }
                    ? new EquatableArray<byte>(InlineImageData)
                    : EquatableArray<byte>.Empty,
                Path = string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath,
                Expression = string.IsNullOrWhiteSpace(ImageExpression) ? null : ImageExpression,
                Sizing = ImageSizing,
            },
            DesignerElementKind.Barcode => new BarcodeElement
            {
                Bounds = Bounds,
                Expression = Expression,
                // Honour the picked 1D symbology; if the user accidentally set QrCode on a
                // Barcode-kind element via direct property binding, force back to Code128 —
                // the QrCode kind is the canonical place for QR.
                Symbology = Symbology == BarcodeSymbology.QrCode ? BarcodeSymbology.Code128 : Symbology,
                ShowText = BarcodeShowText,
            },
            DesignerElementKind.QrCode => new BarcodeElement
            {
                Bounds = Bounds,
                Expression = Expression,
                Symbology = BarcodeSymbology.QrCode,
                QrEcc = QrEcc,
                ShowText = false, // QR has no human-readable text strip
            },
            DesignerElementKind.Chart => new ChartElement
            {
                Bounds = Bounds,
                Kind = ChartKind,
                Title = string.IsNullOrWhiteSpace(ChartTitle) ? null : ChartTitle,
                ShowLegend = ShowLegend,
                Series = ChartSeries.Count == 0
                    ? Reporting.Common.EquatableArray<ChartSeries>.Empty
                    : new Reporting.Common.EquatableArray<ChartSeries>(ChartSeries.Select(s => s.ToSeries())),
            },
            // Advanced elements without a dedicated editor: re-emit the original domain element
            // (preserving all its config) with the designer's current bounds.
            _ when IsOpaqueAdvanced(Kind) => (_sourceElement ?? CreateDefaultAdvanced(Kind)) with { Bounds = Bounds },
            _ => throw new InvalidOperationException($"Unknown kind: {Kind}"),
        };

        var conditionalFormats = ConditionalFormats.Count == 0
            ? Reporting.Common.EquatableArray<ConditionalFormat>.Empty
            : new Reporting.Common.EquatableArray<ConditionalFormat>(
                ConditionalFormats.Select(c => c.ToConditionalFormat()));

        // Build the RDL <Action> if the user has enabled one. The discriminator selects
        // which sub-fields populate the record — other fields are intentionally null so
        // the serializer emits a compact shape.
        ElementAction? action = null;
        if (HasAction)
        {
            action = ActionKind switch
            {
                ActionKind.Hyperlink when !string.IsNullOrWhiteSpace(Hyperlink) =>
                    new ElementAction(ActionKind.Hyperlink, Hyperlink: Hyperlink),
                ActionKind.BookmarkLink when !string.IsNullOrWhiteSpace(BookmarkLinkId) =>
                    new ElementAction(ActionKind.BookmarkLink, BookmarkId: BookmarkLinkId),
                ActionKind.DrillthroughReport when !string.IsNullOrWhiteSpace(DrillthroughReportName) =>
                    new ElementAction(ActionKind.DrillthroughReport,
                        DrillthroughReportName: DrillthroughReportName,
                        DrillthroughParameters: new Reporting.Common.EquatableArray<DrillthroughParameter>(
                            DrillthroughParameters.Select(p => p.ToCoreParameter()))),
                _ => null,
            };
        }

        // Opaque-advanced elements have NO appearance editor, so the flat Style fields are just defaults
        // (Arial/10/Black). Keep the source element's real Style — which may carry null = inherit — instead
        // of materialising it and corrupting the inherited appearance on every metadata edit.
        var effectiveStyle = IsOpaqueAdvanced(Kind) ? element.Style : style;

        return element with
        {
            Id = Id,
            Name = Name,
            Style = effectiveStyle,
            Visible = IsVisible,
            VisibleExpression = string.IsNullOrWhiteSpace(VisibleExpr) ? null : VisibleExpr,
            ConditionalFormats = conditionalFormats,
            PropertyExpressions = _propertyExpressions.Count == 0
                ? Reporting.Common.EquatableDictionary<string, string>.Empty
                : new Reporting.Common.EquatableDictionary<string, string>(_propertyExpressions),
            Bookmark = string.IsNullOrWhiteSpace(Bookmark) ? null : Bookmark,
            DocumentMapLabel = string.IsNullOrWhiteSpace(DocumentMapLabel) ? null : DocumentMapLabel,
            ToggleItemId = string.IsNullOrWhiteSpace(ToggleItemId) ? null : ToggleItemId,
            InitiallyHidden = InitiallyHidden,
            Action = action,
        };
    }

    private static ReportElement CreateDefaultAdvanced(DesignerElementKind kind) => kind switch
    {
        DesignerElementKind.Tablix => new TablixElement(),
        DesignerElementKind.Gauge => new GaugeElement(),
        DesignerElementKind.DataBar => new DataBarElement(),
        DesignerElementKind.Sparkline => new SparklineElement(),
        DesignerElementKind.Indicator => new IndicatorElement(),
        DesignerElementKind.Map => new MapElement(),
        DesignerElementKind.Subreport => new SubreportElement(),
        DesignerElementKind.Code => new CodeElement(),
        _ => new TextBoxElement { Expression = string.Empty },
    };

    /// <summary>Creates a deep copy of this element with a fresh <see cref="Id"/>. Used by the
    /// designer clipboard for copy/paste.</summary>
    public ElementViewModel Clone()
    {
        var c = new ElementViewModel(Kind, Guid.NewGuid().ToString("n"))
        {
            Name = Name,
            Text = Text,
            Expression = Expression,
            X = X, Y = Y, Width = Width, Height = Height,
            IsBold = IsBold, IsItalic = IsItalic, IsUnderline = IsUnderline, IsStrikethrough = IsStrikethrough,
            FontFamily = FontFamily, FontSize = FontSize,
            ForeColor = ForeColor, FillColor = FillColor, BackColor = BackColor,
            HorizontalAlignment = HorizontalAlignment, VerticalAlignment = VerticalAlignment,
            WordWrap = WordWrap, Padding = Padding, Border = Border, Format = Format,
            IsVisible = IsVisible, VisibleExpr = VisibleExpr, CanGrow = CanGrow, CanShrink = CanShrink, KeepTogether = KeepTogether,
            IsLocked = IsLocked, CornerRadiusMm = CornerRadiusMm,
            InlineImageData = InlineImageData is null ? null : (byte[])InlineImageData.Clone(),
            Symbology = Symbology, QrEcc = QrEcc, BarcodeShowText = BarcodeShowText,
            LineDir = LineDir,
            ImageSizing = ImageSizing, ImagePath = ImagePath, ImageExpression = ImageExpression,
        };
        c._textRuns = _textRuns; // EquatableArray is immutable → safe to share on clone
        foreach (var child in Children) // deep-clone container children (fresh ids) so paste duplicates the whole subtree
        {
            c.AttachChild(child.Clone());
        }
        c._backgroundImage = _backgroundImage; // immutable record — safe to share on clone
        foreach (var rule in ConditionalFormats)
        {
            c.ConditionalFormats.Add(new ConditionalFormatRule
            {
                Condition = rule.Condition,
                ForeColor = rule.ForeColor,
                BackColor = rule.BackColor,
                Bold = rule.Bold,
                Italic = rule.Italic,
            });
        }
        c.ChartKind = ChartKind;
        c.ChartTitle = ChartTitle;
        c.ShowLegend = ShowLegend;
        foreach (var s in ChartSeries)
        {
            c.ChartSeries.Add(new ChartSeriesRule
            {
                Name = s.Name,
                CategoryExpression = s.CategoryExpression,
                ValueExpression = s.ValueExpression,
                Color = s.Color,
                SizeExpression = s.SizeExpression,
                HighExpression = s.HighExpression,
                LowExpression = s.LowExpression,
            });
        }

        // Deep-clone the RDL Phase 1 extensions so the clipboard / copy-paste flow doesn't
        // accidentally share mutable parameter rules between source and clone.
        c.Bookmark = Bookmark;
        c.DocumentMapLabel = DocumentMapLabel;
        c.ToggleItemId = ToggleItemId;
        c.InitiallyHidden = InitiallyHidden;
        c.HasAction = HasAction;
        c.ActionKind = ActionKind;
        c.Hyperlink = Hyperlink;
        c.BookmarkLinkId = BookmarkLinkId;
        c.DrillthroughReportName = DrillthroughReportName;
        foreach (var p in DrillthroughParameters)
        {
            c.DrillthroughParameters.Add(new DrillthroughParameterRule { Name = p.Name, Value = p.Value, Omit = p.Omit });
        }
        // Carry the preserved domain element for opaque-advanced kinds (Tablix/Gauge/.../Subreport/
        // Code). It's an immutable record and Mutate<T> replaces the reference rather than editing
        // in place, so sharing it between the original and the clone is safe — and without this a
        // copy/paste would reset the pasted element to its empty default.
        c._sourceElement = _sourceElement;
        foreach (var kv in _propertyExpressions)
        {
            c._propertyExpressions[kv.Key] = kv.Value;
        }
        return c;
    }

    /// <summary>Inverse of <see cref="ToElement"/> — used when loading a definition.</summary>
    public static ElementViewModel FromElement(ReportElement element)
    {
        var kind = element switch
        {
            LabelElement => DesignerElementKind.Label,
            TextBoxElement => DesignerElementKind.TextBox,
            LineElement => DesignerElementKind.Line,
            RectangleElement => DesignerElementKind.Rectangle,
            EllipseElement => DesignerElementKind.Ellipse,
            ImageElement => DesignerElementKind.Image,
            ChartElement => DesignerElementKind.Chart,
            TablixElement => DesignerElementKind.Tablix,
            GaugeElement => DesignerElementKind.Gauge,
            DataBarElement => DesignerElementKind.DataBar,
            SparklineElement => DesignerElementKind.Sparkline,
            IndicatorElement => DesignerElementKind.Indicator,
            MapElement => DesignerElementKind.Map,
            // Subreport / Code have no editor yet but MUST be preserved: mapping them to their
            // own kinds (vs. the TextBox catch-all) routes them through the opaque _sourceElement
            // path so a load → edit → save cycle keeps their config intact.
            SubreportElement => DesignerElementKind.Subreport,
            CodeElement => DesignerElementKind.Code,
            // QR is a separate designer kind so the toolbox / outline / property grid can
            // give it dedicated affordances (ECC picker, square sizing default, no text strip).
            BarcodeElement { Symbology: BarcodeSymbology.QrCode } => DesignerElementKind.QrCode,
            BarcodeElement => DesignerElementKind.Barcode,
            _ => DesignerElementKind.TextBox,
        };
        var vm = new ElementViewModel(kind, element.Id);
        vm.LoadFrom(element);
        return vm;
    }

    /// <summary>Re-hydrates this view model from an immutable <paramref name="element"/> — the inverse of
    /// <see cref="ToElement"/>. Used by <see cref="FromElement"/> on load and by
    /// <see cref="ApplyMetaSet"/> to push a metadata-driven change back in place. Mutable collections are
    /// cleared first so re-loading stays idempotent (no duplicated rows).</summary>
    private void LoadFrom(ReportElement element)
    {
        ConditionalFormats.Clear();
        ChartSeries.Clear();
        DrillthroughParameters.Clear();
        DetachAllChildren();
        _propertyExpressions.Clear();

        var fontStyle = element.Style.Font?.Style ?? FontStyle.Regular;
        Name = element.Name;
        X = element.Bounds.X;
        Y = element.Bounds.Y;
        Width = element.Bounds.Width;
        Height = element.Bounds.Height;
        ForeColor = element.Style.ForeColor ?? Color.Black;
        BackColor = element.Style.BackColor;
        _backgroundImage = element.Style.BackgroundImage; // preserved (no editor yet) so edits don't drop it
        Padding = element.Style.Padding;
        FontFamily = element.Style.Font?.Family ?? "Arial";
        FontSize = element.Style.Font?.Size ?? 10;
        IsBold = (fontStyle & FontStyle.Bold) != 0;
        IsItalic = (fontStyle & FontStyle.Italic) != 0;
        IsUnderline = (fontStyle & FontStyle.Underline) != 0;
        IsStrikethrough = (fontStyle & FontStyle.Strikeout) != 0;
        HorizontalAlignment = element.Style.HorizontalAlignment;
        VerticalAlignment = element.Style.VerticalAlignment;
        WordWrap = element.Style.WordWrap;
        Format = element.Style.Format;
        Border = element.Style.Border;
        IsVisible = element.Visible;
        VisibleExpr = element.VisibleExpression;

        switch (element)
        {
            case LabelElement lbl: Text = lbl.Text; break;
            case TextBoxElement tb:
                Expression = tb.Expression;
                CanGrow = tb.CanGrow;
                CanShrink = tb.CanShrink;
                _textRuns = tb.TextRuns; // preserve mixed-style runs across edit→save (no editor yet)
                break;
            case BarcodeElement bc:
                Expression = bc.Expression;
                Symbology = bc.Symbology;
                QrEcc = bc.QrEcc;
                BarcodeShowText = bc.ShowText;
                break;
            case LineElement ln: LineDir = ln.Direction; break;
            case RectangleElement r:
                FillColor = r.FillColor;
                CornerRadiusMm = r.CornerRadius.ToMm();
                // Materialise children into editable child VMs (recursive — a child Rectangle materialises its
                // own children in turn). The same FromElement path preserves opaque kinds, so depth is unbounded.
                foreach (var child in r.Children)
                {
                    AttachChild(FromElement(child));
                }
                break;
            case EllipseElement e: FillColor = e.FillColor; break;
            case ImageElement img:
                InlineImageData = img.InlineData.Count > 0 ? img.InlineData.ToArray() : null;
                ImagePath = img.Path;
                ImageExpression = img.Expression;
                ImageSizing = img.Sizing;
                break;
            case ChartElement ch:
                ChartKind = ch.Kind;
                ChartTitle = ch.Title ?? string.Empty;
                ShowLegend = ch.ShowLegend;
                foreach (var s in ch.Series)
                {
                    ChartSeries.Add(ChartSeriesRule.From(s));
                }
                break;
        }
        foreach (var cf in element.ConditionalFormats)
        {
            ConditionalFormats.Add(ConditionalFormatRule.From(cf));
        }
        foreach (var kv in element.PropertyExpressions)
        {
            _propertyExpressions[kv.Key] = kv.Value;
        }

        // RDL Phase 1 extensions: pull every additive field back into the VM so the
        // PropertyGrid can edit them. HasAction is derived from the presence of an
        // Action child — matches the "checkbox enables the section" UX.
        Bookmark = element.Bookmark;
        DocumentMapLabel = element.DocumentMapLabel;
        ToggleItemId = element.ToggleItemId;
        InitiallyHidden = element.InitiallyHidden;
        if (element.Action is { } act)
        {
            HasAction = true;
            ActionKind = act.Kind;
            Hyperlink = act.Hyperlink;
            BookmarkLinkId = act.BookmarkId;
            DrillthroughReportName = act.DrillthroughReportName;
            foreach (var p in act.DrillthroughParameters)
            {
                DrillthroughParameters.Add(DrillthroughParameterRule.From(p));
            }
        }
        else
        {
            // No action on the element → clear the editor fields too, so a re-hydration (ApplyMetaSet)
            // can't leave a stale URL/bookmark that would resurface if the action toggle is re-enabled.
            HasAction = false;
            ActionKind = ActionKind.Hyperlink;
            Hyperlink = null;
            BookmarkLinkId = null;
            DrillthroughReportName = null;
        }
        // Preserve the full domain element for advanced kinds without a dedicated editor, so
        // re-saving doesn't degrade them (they'd otherwise fall back to a TextBox).
        _sourceElement = IsOpaqueAdvanced(Kind) ? element : null;
    }
}
