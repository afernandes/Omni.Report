using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.CodeFirst;

/// <summary>
/// Single fluent surface used to describe the contents of any band (report header,
/// page header, group header/footer, detail, page footer, report footer). All element
/// adders (Text, Label, Line, Rectangle, Ellipse, Image) commit any pending element and
/// start a new one; element-config methods (At, Size, Font, Bold, Center, …) mutate the
/// currently pending element.
/// </summary>
/// <remarks>
/// The design trades a small amount of compile-time safety (you can call
/// <see cref="From(double,double)"/> after <see cref="Label(string)"/>) for a much simpler
/// API surface and a chain that exactly matches the spec sample. Mis-targeted calls are
/// silently ignored — switching to a typed element-config builder is a future refinement.
/// </remarks>
public sealed class BandContent
{
    private readonly List<ReportElement> _elements = [];
    private ReportElement? _pending;

    /// <summary>Height of the band in mils. Defaults to the maximum element bottom + 1mm.</summary>
    public Unit BandHeight { get; private set; }

    /// <summary>Whether the detail band can grow / shrink with its content. Ignored for non-detail bands.</summary>
    public bool DetailCanGrow { get; private set; }
    public bool DetailCanShrink { get; private set; }

    /// <summary>Whether report/page header/footer is printed on the first/last page (default true).</summary>
    public bool PrintOnFirstPage { get; private set; } = true;
    public bool PrintOnLastPage { get; private set; } = true;

    /// <summary>Optional visibility expression for the entire band.</summary>
    public string? VisibleExpression { get; private set; }

    /// <summary>RDL PageBreak rule for this band (None default — drives the paginator).</summary>
    public Reporting.Bands.PageBreak PageBreakRule { get; private set; }

    // ── Band-level configuration ────────────────────────────────────────────────

    public BandContent Height(Unit height) { Flush(); BandHeight = height; return this; }
    public BandContent Height(double mm) => Height(Unit.FromMm(mm));
    public BandContent CanGrow(bool value = true) { Flush(); DetailCanGrow = value; return this; }
    public BandContent CanShrink(bool value = true) { Flush(); DetailCanShrink = value; return this; }
    public BandContent NotOnFirstPage() { Flush(); PrintOnFirstPage = false; return this; }
    public BandContent NotOnLastPage() { Flush(); PrintOnLastPage = false; return this; }
    public BandContent VisibleWhen(string expression) { Flush(); VisibleExpression = expression; return this; }

    /// <summary>Sets the band's RDL PageBreak rule. Defaults to None — set to
    /// <see cref="Reporting.Bands.PageBreak.Start"/> for "start on new page",
    /// <see cref="Reporting.Bands.PageBreak.End"/> for "break after this band", etc.</summary>
    public BandContent PageBreak(Reporting.Bands.PageBreak rule) { Flush(); PageBreakRule = rule; return this; }

    // ── Element starters ─────────────────────────────────────────────────────────

    public BandContent Text(string expression)
    {
        Flush();
        _pending = new TextBoxElement { Expression = expression, Bounds = Reporting.Geometry.Rectangle.Empty };
        return this;
    }

    public BandContent Label(string text)
    {
        Flush();
        _pending = new LabelElement { Text = text, Bounds = Reporting.Geometry.Rectangle.Empty };
        return this;
    }

    public BandContent Line()
    {
        Flush();
        _pending = new LineElement { Bounds = Reporting.Geometry.Rectangle.Empty, Direction = LineDirection.Horizontal };
        return this;
    }

    public BandContent Rectangle()
    {
        Flush();
        _pending = new RectangleElement { Bounds = Reporting.Geometry.Rectangle.Empty };
        return this;
    }

    /// <summary>Starts a CONTAINER rectangle and populates its <see cref="RectangleElement.Children"/>
    /// from <paramref name="children"/> — nested elements are positioned RELATIVE to the rectangle's
    /// top-left, drawn on top of its fill (no clipping). Subsequent config calls (At/Size/Fill/CornerRadius)
    /// apply to the rectangle itself.</summary>
    public BandContent Rectangle(Action<BandContent> children)
    {
        Flush();
        var inner = new BandContent();
        children(inner);
        _pending = new RectangleElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Children = inner.BuildElements(),
        };
        return this;
    }

    public BandContent Ellipse()
    {
        Flush();
        _pending = new EllipseElement { Bounds = Reporting.Geometry.Rectangle.Empty };
        return this;
    }

    public BandContent Image(string? path = null, byte[]? bytes = null, string? expression = null)
    {
        Flush();
        var img = new ImageElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Source = bytes is not null ? ImageSourceKind.Inline
                  : expression is not null ? ImageSourceKind.Expression
                  : ImageSourceKind.Path,
            Path = path,
            InlineData = bytes is null ? EquatableArray<byte>.Empty : new EquatableArray<byte>(bytes),
            Expression = expression,
        };
        _pending = img;
        return this;
    }

    public BandContent Barcode(string expression, BarcodeSymbology symbology = BarcodeSymbology.Code128)
    {
        Flush();
        _pending = new BarcodeElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Symbology = symbology,
            Expression = expression,
        };
        return this;
    }

    // ── Common element configuration ─────────────────────────────────────────────

    /// <summary>Position (millimeters from band origin).</summary>
    public BandContent At(double xMm, double yMm)
        => MutatePending(e => e with { Bounds = new Reporting.Geometry.Rectangle(Unit.FromMm(xMm), Unit.FromMm(yMm), e.Bounds.Width, e.Bounds.Height) });

    /// <summary>Size in millimeters.</summary>
    public BandContent Size(double widthMm, double heightMm)
        => MutatePending(e => e with { Bounds = new Reporting.Geometry.Rectangle(e.Bounds.X, e.Bounds.Y, Unit.FromMm(widthMm), Unit.FromMm(heightMm)) });

    public BandContent Bounds(double xMm, double yMm, double widthMm, double heightMm)
        => MutatePending(e => e with { Bounds = new Reporting.Geometry.Rectangle(Unit.FromMm(xMm), Unit.FromMm(yMm), Unit.FromMm(widthMm), Unit.FromMm(heightMm)) });

    public BandContent Name(string name)
        => MutatePending(e => e with { Name = name });

    public BandContent Hidden()
        => MutatePending(e => e with { Visible = false });

    public BandContent VisibleIf(string expression)
        => MutatePending(e => e with { VisibleExpression = expression });

    // ── Text / typography helpers (mutate Style on Pending) ──────────────────────

    public BandContent Font(string family, double size, FontStyle style = FontStyle.Regular)
        => MutateStyle(s => s with { Font = new Font(family, size, style) });

    public BandContent Bold() => MutateStyle(s => s with { Font = (s.Font ?? Reporting.Styling.Font.Default).AddStyle(FontStyle.Bold) });
    public BandContent Italic() => MutateStyle(s => s with { Font = (s.Font ?? Reporting.Styling.Font.Default).AddStyle(FontStyle.Italic) });
    public BandContent Underline() => MutateStyle(s => s with { Font = (s.Font ?? Reporting.Styling.Font.Default).AddStyle(FontStyle.Underline) });

    public BandContent FontSize(double size) => MutateStyle(s => s with { Font = (s.Font ?? Reporting.Styling.Font.Default).WithSize(size) });

    public BandContent Color(Color color) => MutateStyle(s => s with { ForeColor = color });
    public BandContent Background(Color color) => MutateStyle(s => s with { BackColor = color });

    /// <summary>Two-colour gradient background: <paramref name="start"/> blends to <paramref name="end"/>
    /// along <paramref name="type"/> (default top→bottom; <see cref="BackgroundGradientType.Center"/> is radial).</summary>
    public BandContent BackgroundGradient(Color start, Color end, BackgroundGradientType type = BackgroundGradientType.TopBottom)
        => MutateStyle(s => s with { BackColor = start, BackColorEnd = end, BackgroundGradient = type });

    /// <summary>Inherits a report-level named style (defined via <c>ReportBuilderRoot.NamedStyle</c>): it becomes the
    /// base, and any inline style set here overlays it. See <see cref="Style.BasedOn"/>.</summary>
    public BandContent BasedOn(string namedStyle) => MutateStyle(s => s with { BasedOn = namedStyle });

    public BandContent Center() => MutateStyle(s => s with { HorizontalAlignment = HorizontalAlignment.Center });
    public BandContent AlignLeft() => MutateStyle(s => s with { HorizontalAlignment = HorizontalAlignment.Left });
    public BandContent AlignRight() => MutateStyle(s => s with { HorizontalAlignment = HorizontalAlignment.Right });
    public BandContent AlignTop() => MutateStyle(s => s with { VerticalAlignment = VerticalAlignment.Top });
    public BandContent AlignMiddle() => MutateStyle(s => s with { VerticalAlignment = VerticalAlignment.Middle });
    public BandContent AlignBottom() => MutateStyle(s => s with { VerticalAlignment = VerticalAlignment.Bottom });

    public BandContent NoWrap() => MutateStyle(s => s with { WordWrap = false });
    public BandContent Format(string format) => MutateStyle(s => s with { Format = format });

    public BandContent Border(Border border) => MutateStyle(s => s with { Border = border });
    public BandContent Border(BorderLineStyle style, double thicknessPt, Color color)
        => MutateStyle(s => s with { Border = Reporting.Styling.Border.Uniform(style, Unit.FromPoint(thicknessPt), color) });

    // ── TextBox-specific ────────────────────────────────────────────────────────

    public BandContent ElementCanGrow(bool value = true)
        => MutatePending(e => e is TextBoxElement tb ? tb with { CanGrow = value } : e);

    public BandContent ElementCanShrink(bool value = true)
        => MutatePending(e => e is TextBoxElement tb ? tb with { CanShrink = value } : e);

    public BandContent ConditionalFormat(string condition, Style style)
        => MutatePending(e => e with { ConditionalFormats = new EquatableArray<ConditionalFormat>(
            e.ConditionalFormats.Concat(new[] { new ConditionalFormat(condition, style) })) });

    // ── RDL element-level extensions ────────────────────────────────────────────

    /// <summary>RDL <c>&lt;Bookmark&gt;</c>: anchor that another element's Action.BookmarkLink
    /// can jump to (PDF named destination, HTML id).</summary>
    public BandContent Bookmark(string id)
        => MutatePending(e => e with { Bookmark = id });

    /// <summary>RDL <c>&lt;Label&gt;</c> (DocumentMap label): adds this element to the
    /// document map / table-of-contents shown by interactive viewers.</summary>
    public BandContent DocumentMapLabel(string label)
        => MutatePending(e => e with { DocumentMapLabel = label });

    /// <summary>RDL <c>&lt;Action&gt;&lt;Hyperlink&gt;</c>: clicking the element opens the URL
    /// (literal or expression-produced). Mutually exclusive with the other action helpers.</summary>
    public BandContent Hyperlink(string urlOrExpression)
        => MutatePending(e => e with { Action = ElementAction.ToUrl(urlOrExpression) });

    /// <summary>RDL <c>&lt;Action&gt;&lt;BookmarkLink&gt;</c>: clicking the element jumps to
    /// the element whose <see cref="ReportElement.Bookmark"/> equals <paramref name="bookmarkId"/>.</summary>
    public BandContent LinkToBookmark(string bookmarkId)
        => MutatePending(e => e with { Action = ElementAction.ToBookmark(bookmarkId) });

    /// <summary>RDL <c>&lt;Action&gt;&lt;Drillthrough&gt;</c>: clicking the element opens
    /// <paramref name="reportName"/>, passing the provided parameters.</summary>
    public BandContent Drillthrough(string reportName, params DrillthroughParameter[] parameters)
        => MutatePending(e => e with { Action = ElementAction.ToDrillthrough(reportName, parameters) });

    /// <summary>RDL <c>&lt;Visibility&gt;&lt;ToggleItem&gt;</c>: when set, the element identified
    /// by <paramref name="bookmarkOfToggle"/> renders an expand/collapse chevron that toggles
    /// THIS element's visibility (drill-down).</summary>
    public BandContent ToggleItem(string bookmarkOfToggle)
        => MutatePending(e => e with { ToggleItemId = bookmarkOfToggle });

    /// <summary>RDL <c>&lt;Visibility&gt;&lt;Hidden&gt;true&lt;/Hidden&gt;</c>: the element
    /// starts collapsed when a ToggleItem is set; the user expands it interactively.</summary>
    public BandContent StartHidden()
        => MutatePending(e => e with { InitiallyHidden = true });

    // ── Line-specific ───────────────────────────────────────────────────────────

    public BandContent From(double xMm, double yMm)
        => MutatePending(e => e is LineElement
            ? e with { Bounds = new Reporting.Geometry.Rectangle(Unit.FromMm(xMm), Unit.FromMm(yMm), e.Bounds.Width, e.Bounds.Height) }
            : e);

    public BandContent To(double xMm, double yMm)
        => MutatePending(e =>
        {
            if (e is not LineElement)
            {
                return e;
            }
            var width = Unit.FromMm(xMm) - e.Bounds.X;
            var height = Unit.FromMm(yMm) - e.Bounds.Y;
            return e with { Bounds = new Reporting.Geometry.Rectangle(e.Bounds.X, e.Bounds.Y, width, height) };
        });

    public BandContent Direction(LineDirection direction)
        => MutatePending(e => e is LineElement line ? line with { Direction = direction } : e);

    public BandContent Thickness(double pt)
        => MutatePending(e => e is LineElement line
            ? line with { Pen = line.Pen with { Thickness = Unit.FromPoint(pt) } }
            : e);

    // ── Rectangle/Ellipse-specific ──────────────────────────────────────────────

    public BandContent Fill(Color color)
        => MutatePending(e => e switch
        {
            RectangleElement r => r with { FillColor = color },
            EllipseElement el => el with { FillColor = color },
            _ => e,
        });

    public BandContent CornerRadius(double mm)
        => MutatePending(e => e is RectangleElement r ? r with { CornerRadius = Unit.FromMm(mm) } : e);

    // ── Image-specific ──────────────────────────────────────────────────────────

    public BandContent ImageSizing(ImageSizing sizing)
        => MutatePending(e => e is ImageElement i ? i with { Sizing = sizing } : e);

    // ── Chart-specific ──────────────────────────────────────────────────────────

    /// <summary>Starts a chart element. Add data with <see cref="Series(string,string,string,Color?)"/>;
    /// the series bind to the report's primary data source. Place charts in a report/page
    /// header or footer (not the detail band, which would re-emit the chart per row).</summary>
    public BandContent Chart(ChartKind kind = ChartKind.Bar, string? title = null)
    {
        Flush();
        _pending = new ChartElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Kind = kind,
            Title = title,
        };
        return this;
    }

    /// <summary>Adds a data series to the pending chart. <paramref name="categoryExpression"/>
    /// and <paramref name="valueExpression"/> are NCalc expressions evaluated per row
    /// (e.g. <c>"Fields.mes"</c>, <c>"Fields.total"</c>); rows that share a category are summed.</summary>
    public BandContent Series(string name, string categoryExpression, string valueExpression, Color? color = null)
        => MutatePending(e => e is ChartElement c
            ? c with { Series = new EquatableArray<ChartSeries>(
                c.Series.Append(new ChartSeries(name, categoryExpression, valueExpression, color))) }
            : e);

    /// <summary>Adds a bubble series — <paramref name="xExpression"/> is the category/X,
    /// <paramref name="yExpression"/> the value/Y, and <paramref name="sizeExpression"/> scales each
    /// marker's radius. Use with <see cref="ChartKind.Bubble"/>.</summary>
    public BandContent BubbleSeries(string name, string xExpression, string yExpression, string sizeExpression, Color? color = null)
        => MutatePending(e => e is ChartElement c
            ? c with { Series = new EquatableArray<ChartSeries>(
                c.Series.Append(new ChartSeries(name, xExpression, yExpression, color, SizeExpression: sizeExpression))) }
            : e);

    /// <summary>Adds a stock series — a high-low range per category with a close tick at
    /// <paramref name="closeExpression"/>. Use with <see cref="ChartKind.Stock"/>.</summary>
    public BandContent StockSeries(string name, string categoryExpression, string highExpression, string lowExpression, string closeExpression, Color? color = null)
        => MutatePending(e => e is ChartElement c
            ? c with { Series = new EquatableArray<ChartSeries>(
                c.Series.Append(new ChartSeries(name, categoryExpression, closeExpression, color, HighExpression: highExpression, LowExpression: lowExpression))) }
            : e);

    /// <summary>Shows or hides the chart legend (shown by default).</summary>
    public BandContent Legend(bool show = true)
        => MutatePending(e => e is ChartElement c ? c with { ShowLegend = show } : e);

    // ── KPI elements (Gauge / DataBar) ───────────────────────────────────────────

    /// <summary>Starts a data bar — a horizontal bar that fills proportionally to
    /// <paramref name="valueExpression"/> within its min/max range (set via <see cref="Range"/>).
    /// Typically placed in a detail cell to visualise a per-row metric.</summary>
    public BandContent DataBar(string valueExpression, string fillColorHex = "#C2410C")
    {
        Flush();
        _pending = new DataBarElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            ValueExpression = valueExpression,
            FillColor = fillColorHex,
        };
        return this;
    }

    /// <summary>Starts a gauge showing <paramref name="valueExpression"/> against a range.
    /// The value is evaluated once per band, so an aggregate like <c>"Sum(Fields.Total)"</c>
    /// in a group/report footer shows that scope's total. Add coloured zones with
    /// <see cref="GaugeBand"/>.</summary>
    public BandContent Gauge(string valueExpression, GaugeKind kind = GaugeKind.Radial)
    {
        Flush();
        _pending = new GaugeElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Kind = kind,
            ValueExpression = valueExpression,
        };
        return this;
    }

    /// <summary>Sets the numeric min/max range of the pending gauge or data bar.</summary>
    public BandContent Range(double min, double max)
        => MutatePending(e => e switch
        {
            GaugeElement g => g with
            {
                MinimumExpression = min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaximumExpression = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            DataBarElement d => d with
            {
                MinimumExpression = min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaximumExpression = max.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            _ => e,
        });

    /// <summary>Adds a coloured zone (e.g. red/amber/green band) to the pending gauge.</summary>
    public BandContent GaugeBand(double start, double end, string colorHex)
        => MutatePending(e => e is GaugeElement g
            ? g with { Ranges = new EquatableArray<GaugeRange>(g.Ranges.Append(new GaugeRange(
                start.ToString(System.Globalization.CultureInfo.InvariantCulture),
                end.ToString(System.Globalization.CultureInfo.InvariantCulture),
                colorHex))) }
            : e);

    /// <summary>Starts a sparkline — a compact trend chart (no axes/labels) over the rows of
    /// <paramref name="dataSet"/> (or the primary source when null). Each row contributes one
    /// point via <paramref name="valueExpression"/>.</summary>
    public BandContent Sparkline(string valueExpression, SparklineKind kind = SparklineKind.Line, string? dataSet = null)
    {
        Flush();
        _pending = new SparklineElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Kind = kind,
            ValueExpression = valueExpression,
            DataSetName = dataSet,
        };
        return this;
    }

    /// <summary>Starts a KPI indicator — an icon (arrow/shape/rating) chosen by which state
    /// range <paramref name="valueExpression"/> falls into. Add ranges with <see cref="State"/>.</summary>
    public BandContent Indicator(string valueExpression, IndicatorKind kind = IndicatorKind.DirectionalArrow)
    {
        Flush();
        _pending = new IndicatorElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            Kind = kind,
            ValueExpression = valueExpression,
        };
        return this;
    }

    /// <summary>Adds a state range to the pending indicator (states are ordered low → high;
    /// the matched state's position drives the icon direction/colour).</summary>
    public BandContent State(double start, double end, string iconName = "")
        => MutatePending(e => e is IndicatorElement ind
            ? ind with { States = new EquatableArray<IndicatorState>(ind.States.Append(new IndicatorState(
                start.ToString(System.Globalization.CultureInfo.InvariantCulture),
                end.ToString(System.Globalization.CultureInfo.InvariantCulture),
                iconName))) }
            : e);

    // ── Tablix (banded table) ─────────────────────────────────────────────────────

    /// <summary>Starts a banded table (RDL Table data region). Define columns inside
    /// <paramref name="configure"/>; the table binds to a data source (via
    /// <see cref="TablixBuilder.DataSet"/> or the primary source) and repeats the detail row
    /// per record. <see cref="Size"/> sets the width; the table auto-grows in height.</summary>
    public BandContent Tablix(Action<TablixBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Flush();
        var builder = new TablixBuilder();
        configure(builder);
        _pending = builder.Build();
        return this;
    }

    /// <summary>Starts a map element that plots one marker per data row at
    /// <paramref name="latitudeExpression"/>/<paramref name="longitudeExpression"/>, normalised
    /// into the element bounds. Binds to <paramref name="dataSet"/> or the primary source.</summary>
    public BandContent Map(string latitudeExpression, string longitudeExpression, string? dataSet = null)
    {
        Flush();
        _pending = new MapElement
        {
            Bounds = Reporting.Geometry.Rectangle.Empty,
            LatitudeExpression = latitudeExpression,
            LongitudeExpression = longitudeExpression,
            DataSetName = dataSet,
        };
        return this;
    }

    /// <summary>Sets the map's vector basemap from inline GeoJSON (FeatureCollection / Feature /
    /// Geometry). Polygons are filled; line strings are stroked. No-op unless the pending element
    /// is a <see cref="MapElement"/>.</summary>
    public BandContent Shapes(string geoJson)
        => MutatePending(e => e is MapElement m ? m with { ShapesGeoJson = geoJson } : e);

    /// <summary>Uses a registered shape set (resolved at render time via the map shape registry,
    /// e.g. <c>"brazil"</c>) as the vector basemap.</summary>
    public BandContent ShapeSet(string name)
        => MutatePending(e => e is MapElement m ? m with { ShapeSet = name } : e);

    /// <summary>Draws a latitude/longitude graticule behind the map so it reads geographically
    /// even without shapes.</summary>
    public BandContent Graticule(bool show = true)
        => MutatePending(e => e is MapElement m ? m with { ShowGraticule = show } : e);

    /// <summary>Overrides the shape fill (land) and stroke (outline) colours — hex strings.</summary>
    public BandContent ShapeColors(string fill, string stroke)
        => MutatePending(e => e is MapElement m ? m with { ShapeFill = fill, ShapeStroke = stroke } : e);

    /// <summary>Sets a raster basemap provider / tile URL template (e.g.
    /// <c>"https://tile.openstreetmap.org/{z}/{x}/{y}.png"</c>). The engine computes the visible tile
    /// grid and asks <c>PaginationRequest.MapTileResolver</c> for the bytes — left unwired, the map
    /// renders vector-only. Network fetching lives in the host / an opt-in provider.</summary>
    public BandContent Basemap(string providerOrUrlTemplate)
        => MutatePending(e => e is MapElement m ? m with { Basemap = providerOrUrlTemplate } : e);

    /// <summary>Embeds a child report referenced by id at this element's bounds. The id is resolved
    /// at pagination time via <c>PaginationRequest.SubreportResolver</c>; the child paginates at the
    /// subreport's width against the parent's data sources. Use <see cref="SubreportInline"/> for an
    /// inline definition and <see cref="SubreportParameter"/> to bind child parameters.</summary>
    public BandContent Subreport(string reportId)
    {
        Flush();
        _pending = new SubreportElement { Bounds = Reporting.Geometry.Rectangle.Empty, ReportId = reportId };
        return this;
    }

    /// <summary>Embeds an inline child report definition at this element's bounds.</summary>
    public BandContent SubreportInline(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Flush();
        _pending = new SubreportElement { Bounds = Reporting.Geometry.Rectangle.Empty, InlineDefinition = definition };
        return this;
    }

    /// <summary>Binds a child report parameter to an expression evaluated in the parent context.
    /// No-op unless the pending element is a <see cref="SubreportElement"/>.</summary>
    public BandContent SubreportParameter(string name, string valueExpression)
        => MutatePending(e => e is SubreportElement s
            ? s with { ParameterBindings = AddBinding(s.ParameterBindings, name, valueExpression) }
            : e);

    /// <summary>Binds the child's data to an expression in the parent context. No-op unless the
    /// pending element is a <see cref="SubreportElement"/>.</summary>
    public BandContent SubreportData(string dataExpression)
        => MutatePending(e => e is SubreportElement s ? s with { DataExpression = dataExpression } : e);

    /// <summary>Binds a property of the pending element to a report <b>expression</b> evaluated per
    /// instance at render time, overriding the property's static value (SSRS-style). The
    /// <paramref name="propertyPath"/> may be nested: <c>"Direction"</c>, <c>"FillColor"</c>,
    /// <c>"Style.ForeColor"</c>, <c>"Style.Font.Size"</c>, <c>"Bounds.Width"</c>, <c>"Visible"</c>. The
    /// static value (set the normal way) remains the fallback when the expression fails. Works on any
    /// element; static and bound values coexist on the same element.</summary>
    public BandContent Bind(string propertyPath, string expression)
        => MutatePending(e => e with { PropertyExpressions = AddBinding(e.PropertyExpressions, propertyPath, expression) });

    private static EquatableDictionary<string, string> AddBinding(
        EquatableDictionary<string, string> existing, string key, string value)
    {
        var dict = existing.ToDictionary(kv => kv.Key, kv => kv.Value);
        dict[key] = value;
        return new EquatableDictionary<string, string>(dict);
    }

    // ── Internals ───────────────────────────────────────────────────────────────

    internal EquatableArray<ReportElement> BuildElements()
    {
        Flush();
        return new EquatableArray<ReportElement>(_elements);
    }

    private BandContent MutatePending(Func<ReportElement, ReportElement> mutator)
    {
        if (_pending is not null)
        {
            _pending = mutator(_pending);
        }
        return this;
    }

    private BandContent MutateStyle(Func<Style, Style> mutator)
        => MutatePending(e => e with { Style = mutator(e.Style) });

    private void Flush()
    {
        if (_pending is not null)
        {
            _elements.Add(_pending);
            _pending = null;
        }
    }
}
