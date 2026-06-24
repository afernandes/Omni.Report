using System.Collections.Concurrent;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

/// <summary>
/// Converts a single <see cref="IBand"/> + expression context + origin into a list of
/// positioned <see cref="LayoutPrimitive"/> instances, and reports the band's actual height
/// (which can differ from the declared height when CanGrow / CanShrink kick in).
/// </summary>
internal sealed class BandRenderer
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly TemplateRenderer _templates;
    private readonly ITextMeasurer _measurer;
    private readonly IReadOnlyDictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>> _dataSources;
    private readonly string? _primarySource;

    /// <summary>Renders a <see cref="SubreportElement"/> at the given absolute bounds into a list of
    /// primitives. Supplied by the paginator (which can recurse into a child report); null in
    /// contexts that don't support subreports (the element then renders nothing).</summary>
    private readonly Func<SubreportElement, Rectangle, IReportExpressionContext, IReadOnlyList<LayoutPrimitive>>? _renderSubreport;

    /// <summary>Resolves a Web-Mercator basemap tile to image bytes. Null = vector-only maps.</summary>
    private readonly Func<MapTileRequest, byte[]?>? _mapTileResolver;

    public BandRenderer(
        ExpressionEvaluator evaluator,
        TemplateRenderer templates,
        ITextMeasurer measurer,
        IReadOnlyDictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>>? dataSources = null,
        string? primarySource = null,
        Func<SubreportElement, Rectangle, IReportExpressionContext, IReadOnlyList<LayoutPrimitive>>? renderSubreport = null,
        Func<MapTileRequest, byte[]?>? mapTileResolver = null)
    {
        _evaluator = evaluator;
        _templates = templates;
        _measurer = measurer;
        _dataSources = dataSources ?? new Dictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>>();
        _primarySource = primarySource;
        _renderSubreport = renderSubreport;
        _mapTileResolver = mapTileResolver;
    }

    /// <summary>Renders <paramref name="band"/> at the given origin and returns the resulting
    /// primitives along with the actual band height.</summary>
    public BandLayout Render(IBand band, Point origin, IReportExpressionContext ctx)
    {
        var primitives = new List<LayoutPrimitive>();
        // Accumulate the true content extent from zero (each element's effective bottom, grown/shrunk).
        // The declared band.Height is applied as a FLOOR afterwards — unless the band opts into shrinking.
        Unit contentExtent = Unit.Zero;

        foreach (var rawElement in band.Elements)
        {
            contentExtent = RenderElement(rawElement, origin, origin, primitives, ctx, contentExtent);
        }

        // Band-level CanShrink (opt-in) lets the band collapse below its declared height to wrap its content,
        // pulling the next band up. Without it, band.Height stays the floor (historical grow-only behaviour).
        return new BandLayout(primitives, EffectiveBandHeight(band, contentExtent));
    }

    /// <summary>Renders a SUBSET of a band's elements at a shifted <paramref name="origin"/>. The paginator's
    /// band-split path uses this to emit one slice of an oversized band per page: passing
    /// <c>origin.Y = pageY - sliceTop</c> rebases an element at band-Y <c>ey</c> to <c>pageY + (ey - sliceTop)</c>,
    /// so the slice starts at the top of the new page. Returns the primitives only — the caller controls how
    /// much vertical space the slice consumes.</summary>
    public IReadOnlyList<LayoutPrimitive> RenderElements(IEnumerable<ReportElement> elements, Point origin, IReportExpressionContext ctx)
    {
        var primitives = new List<LayoutPrimitive>();
        Unit ignored = Unit.Zero;
        foreach (var rawElement in elements)
        {
            ignored = RenderElement(rawElement, origin, origin, primitives, ctx, ignored);
        }
        return primitives;
    }

    /// <summary>The effective bottom of a single element in band-space (its <c>Bounds.Y</c> + the height it
    /// will actually render at, honouring CanGrow/CanShrink on a TextBox and the same effective style the
    /// renderer uses). Shared by <see cref="Measure"/> and the paginator's split cut so both agree.
    /// An invisible element contributes nothing (returns zero).</summary>
    internal Unit EffectiveElementBottom(ReportElement rawElement, IReportExpressionContext ctx)
    {
        var element = rawElement.PropertyExpressions.Count == 0
            ? rawElement
            : ApplyPropertyExpressions(rawElement, ctx);
        if (!IsVisible(element, ctx))
        {
            return Unit.Zero;
        }
        if (element is TextBoxElement tb && (tb.CanGrow || tb.CanShrink))
        {
            var effectiveStyle = ResolveEffectiveStyle(element, ctx);
            var style = BuildTextStyle(effectiveStyle);
            var text = ResolveTextBoxText(tb, ctx, effectiveStyle.Format);
            var size = _measurer.Measure(text, style, element.Bounds.Width);
            var h = element.Bounds.Height;
            if (tb.CanGrow && size.Height > h)
            {
                h = size.Height;
            }
            else if (tb.CanShrink && size.Height < h)
            {
                h = size.Height;
            }
            return element.Bounds.Y + h;
        }
        return element.Bounds.Bottom;
    }

    /// <summary>Whether a band may collapse below its declared height to fit its content. Opt-in via
    /// <see cref="DetailBand.CanShrink"/>; other band kinds keep their declared height as the floor.</summary>
    private static bool BandAllowsShrink(IBand band)
        => band is DetailBand { CanShrink: true } && band.Elements.All(IsShrinkSafe);

    /// <summary>Whether an element's rendered height is exactly what <see cref="Measure"/> predicts. Shrinking
    /// a band drops the declared-height floor, so the page-fit pre-check (Measure) MUST equal the emitted
    /// height (Render); elements that grow beyond their declared bounds at render time (a Tablix to its grid
    /// height, a container Rectangle to its children) would break that equality, so a band containing one
    /// keeps its declared height instead of shrinking. Text/Label/Line/Image/Chart/etc. measure exactly.</summary>
    private static bool IsShrinkSafe(ReportElement e) => e switch
    {
        TablixElement => false,
        RectangleElement r when r.Children.Count > 0 => false,
        _ => true,
    };

    /// <summary>Final band height: the content extent when the band opts into shrinking (and actually has
    /// content), otherwise the declared height as a floor. Single source of truth shared by Render and
    /// Measure so the page-fit pre-check and the emission never disagree.</summary>
    private static Unit EffectiveBandHeight(IBand band, Unit contentExtent)
        => BandAllowsShrink(band) && contentExtent > Unit.Zero ? contentExtent : Max(band.Height, contentExtent);

    private static Unit Max(Unit a, Unit b) => a > b ? a : b;

    /// <summary>Renders a single element — and, for a container <see cref="RectangleElement"/>, its
    /// <see cref="RectangleElement.Children"/> recursively — at <paramref name="parentOffset"/>, appending the
    /// resulting primitives and returning the updated band height. Children are positioned RELATIVE to their
    /// rectangle (<paramref name="parentOffset"/> shifts to the rect's top-left for the nested call), while
    /// <paramref name="bandOrigin"/> stays fixed so a nested child still grows the band correctly. No clipping
    /// — children that overflow the rectangle overflow (parity with the legacy flattened behaviour).</summary>
    private Unit RenderElement(ReportElement rawElement, Point parentOffset, Point bandOrigin,
        List<LayoutPrimitive> primitives, IReportExpressionContext ctx, Unit actualHeight)
    {
        // Apply per-property expression bindings first, so visibility/bounds/style overrides take
        // effect before everything below. No bindings → zero cost, same instance.
        var element = rawElement.PropertyExpressions.Count == 0
            ? rawElement
            : ApplyPropertyExpressions(rawElement, ctx);

        if (!IsVisible(element, ctx))
        {
            return actualHeight;
        }
        var elementBounds = new Rectangle(
            parentOffset.X + element.Bounds.X,
            parentOffset.Y + element.Bounds.Y,
            element.Bounds.Width,
            element.Bounds.Height);

            var effectiveStyle = ResolveEffectiveStyle(element, ctx);
            var style = BuildTextStyle(effectiveStyle);

            // Style.BackColor paints as a background fill behind the element (previously dropped everywhere
            // except Tablix cells). Emitted before the content so text/image draws on top; its bounds are
            // patched below if the element grows (CanGrow), so the fill always matches the final size.
            int bgIndex = -1;
            if (effectiveStyle.BackColor is { } backColor)
            {
                bgIndex = primitives.Count;
                primitives.Add(new DrawRectanglePrimitive
                {
                    Bounds = elementBounds,
                    SourceElementId = element.Id,
                    // backColor is the gradient start; the renderer paints a solid fill unless a direction is set.
                    Fill = new BrushStyle(backColor, effectiveStyle.BackColorEnd, effectiveStyle.BackgroundGradient),
                });
            }

            // Style.BackgroundImage paints behind the content (on top of any BackColor), stretched to bounds.
            // Like BackColor, it sits before linkFrom so it gets no Action/link, and is grown with CanGrow below.
            int bgImageIndex = -1;
            if (effectiveStyle.BackgroundImage is { } bgImage)
            {
                var bgBytes = ResolveBackgroundBytes(bgImage, ctx);
                if (bgBytes.Count > 0)
                {
                    bgImageIndex = primitives.Count;
                    primitives.Add(new DrawImagePrimitive
                    {
                        Bounds = elementBounds,
                        SourceElementId = element.Id,
                        Data = bgBytes,
                        Sizing = ImageSizing.Stretch,
                    });
                }
            }

            int linkFrom = primitives.Count;
            // Upper bound for THIS element's Action/Bookmark/DocMapLabel propagation. A container rectangle
            // appends its children's primitives to the same list, but each child already ran its own tail
            // during recursion — so the parent's tail must stop before them, or it would clobber/leak the
            // rect's link onto every child. The rect arm sets this to its pre-children count; -1 = "all".
            int linkTo = -1;
            switch (element)
            {
                case LabelElement lbl:
                    primitives.Add(EmitText(lbl.Text, elementBounds, style, lbl.Id));
                    RecordReportItem(lbl, lbl.Text, ctx);
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, growsTo: null);
                    break;

                case TextBoxElement tb:
                    var text = ResolveTextBoxText(tb, ctx, effectiveStyle.Format);
                    var rendered = EmitText(text, elementBounds, style, tb.Id, tb.CanGrow, tb.CanShrink);
                    primitives.Add(rendered);
                    // Grow/shrink the background fill to the textbox's final height so it never clips.
                    if (bgIndex >= 0 && rendered.Bounds.Height != elementBounds.Height)
                    {
                        primitives[bgIndex] = ((DrawRectanglePrimitive)primitives[bgIndex]) with { Bounds = rendered.Bounds };
                    }
                    if (bgImageIndex >= 0 && rendered.Bounds.Height != elementBounds.Height)
                    {
                        primitives[bgImageIndex] = ((DrawImagePrimitive)primitives[bgImageIndex]) with { Bounds = rendered.Bounds };
                    }
                    // Publish this text box's value for ReportItems!Name.Value lookups in later bands.
                    RecordReportItem(tb, text, ctx);
                    actualHeight = MaxHeight(actualHeight, rendered.Bounds, bandOrigin, growsTo: tb.CanGrow ? rendered.Bounds : null);
                    break;

                case LineElement line:
                    primitives.Add(EmitLine(line, elementBounds));
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case RectangleElement rect:
                    primitives.Add(new DrawRectanglePrimitive
                    {
                        Bounds = elementBounds,
                        SourceElementId = rect.Id,
                        Pen = ResolveBorderPen(rect),
                        Fill = rect.FillColor is { } c ? new BrushStyle(c) : null,
                    });
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    // The rect owns ONLY its fill for link/bookmark purposes — children below get their own.
                    linkTo = primitives.Count;
                    int clipFrom = primitives.Count;
                    // Container: draw children ON TOP of the fill (z-order by construction), positioned
                    // RELATIVE to this rectangle's top-left.
                    foreach (var child in rect.Children)
                    {
                        actualHeight = RenderElement(child, new Point(elementBounds.X, elementBounds.Y),
                                                     bandOrigin, primitives, ctx, actualHeight);
                    }
                    // Clip every child primitive to this rectangle so overflow is cut. For a rect nested in a
                    // rect, intersect with the inner clip already set so the tighter of the two wins. The corner
                    // radius rounds the clip for a DIRECT child; on a nested intersection the rounded-rect∩rect
                    // shape is ill-defined, so fall back to a square clip (radius 0) — a minor visual edge.
                    for (int i = clipFrom; i < primitives.Count; i++)
                    {
                        var existing = primitives[i].ClipBounds;
                        var clip = existing is { } e ? IntersectRect(e, elementBounds) : elementBounds;
                        var radius = existing is null ? rect.CornerRadius : Unit.Zero;
                        primitives[i] = primitives[i] with { ClipBounds = clip, ClipCornerRadius = radius };
                    }
                    break;

                case EllipseElement ellipse:
                    primitives.Add(new DrawEllipsePrimitive
                    {
                        Bounds = elementBounds,
                        SourceElementId = ellipse.Id,
                        Pen = ResolveBorderPen(ellipse),
                        Fill = ellipse.FillColor is { } cf ? new BrushStyle(cf) : null,
                    });
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case ImageElement image:
                    var bytes = ResolveImageBytes(image, ctx);
                    if (bytes.Count > 0)
                    {
                        primitives.Add(new DrawImagePrimitive
                        {
                            Bounds = elementBounds,
                            SourceElementId = image.Id,
                            Data = bytes,
                            Sizing = image.Sizing,
                        });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case BarcodeElement barcode:
                    var bcValue = ResolveText(barcode.Expression, ctx);
                    foreach (var p in BarcodeRenderer.Render(barcode, elementBounds, bcValue,
                                                             style.ForeColor, style, out _))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case ChartElement chart:
                    foreach (var p in ChartRenderer.Render(chart, elementBounds, ResolveRows(), _evaluator, ctx, out _))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case SparklineElement sparkline:
                    foreach (var p in KpiRenderer.RenderSparkline(sparkline, elementBounds, ResolveRows(sparkline.DataSetName), _evaluator, ctx))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case IndicatorElement indicator:
                    foreach (var p in KpiRenderer.RenderIndicator(indicator, elementBounds, _evaluator, ctx))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case DataBarElement dataBar:
                    foreach (var p in KpiRenderer.RenderDataBar(dataBar, elementBounds, _evaluator, ctx))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case GaugeElement gauge:
                    foreach (var p in KpiRenderer.RenderGauge(gauge, elementBounds, _evaluator, ctx))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case TablixElement tablix:
                    var tablixPrims = TablixRenderer.Render(tablix, elementBounds, ResolveRows(tablix.DataSetName),
                                                            _evaluator, _templates, ctx, out var tablixHeight);
                    foreach (var p in tablixPrims)
                    {
                        primitives.Add(p);
                    }
                    var tablixGrown = new Rectangle(elementBounds.X, elementBounds.Y, elementBounds.Width, tablixHeight);
                    actualHeight = MaxHeight(actualHeight, tablixGrown, bandOrigin, tablixGrown);
                    break;

                case MapElement mapEl:
                    foreach (var p in MapRenderer.Render(mapEl, elementBounds, ResolveRows(mapEl.DataSetName), _evaluator, ctx, _mapTileResolver))
                    {
                        primitives.Add(p with { IsVisual = true });
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;

                case SubreportElement subreport when _renderSubreport is not null:
                    foreach (var p in _renderSubreport(subreport, elementBounds, ctx))
                    {
                        primitives.Add(p);
                    }
                    actualHeight = MaxHeight(actualHeight, elementBounds, bandOrigin, null);
                    break;
            }

            // Propagate the element's Action/Bookmark onto every primitive it just emitted, so
            // interactivity-aware exporters (HTML) can wrap them in clickable links / anchors.
            var linkTarget = ResolveLink(element, ctx);
            // A document-map label is literal text (it may carry {expr} placeholders) — render
            // it through the template engine only when it has any, never as a bare expression.
            var docMapLabel = string.IsNullOrWhiteSpace(element.DocumentMapLabel) ? null
                : TemplateRenderer.HasPlaceholders(element.DocumentMapLabel) ? _templates.Render(element.DocumentMapLabel, ctx)
                : element.DocumentMapLabel;
            // Bookmark wins as the anchor; a document-map entry without an explicit bookmark still
            // needs one to link to, so synthesise "dm-<id>".
            var bookmarkId = !string.IsNullOrWhiteSpace(element.Bookmark) ? "bm-" + element.Bookmark
                : docMapLabel is not null ? "dm-" + element.Id
                : null;
            if (linkTarget is not null || bookmarkId is not null || docMapLabel is not null)
            {
                // Scope to THIS element's own primitives — a container rect's children (appended after
                // linkTo during recursion) keep their own link/bookmark and never inherit the parent's.
                int linkEnd = linkTo < 0 ? primitives.Count : linkTo;
                for (int li = linkFrom; li < linkEnd; li++)
                {
                    primitives[li] = primitives[li] with
                    {
                        LinkTarget = linkTarget ?? primitives[li].LinkTarget,
                        BookmarkId = bookmarkId ?? primitives[li].BookmarkId,
                        DocMapLabel = docMapLabel ?? primitives[li].DocMapLabel,
                    };
                }
            }

        return actualHeight;
    }

    /// <summary>Resolves the element's <c>Action</c> to a navigation target string: a hyperlink URL
    /// (its expression is evaluated in <paramref name="ctx"/>), <c>"#bm-&lt;id&gt;"</c> for a bookmark
    /// link, or a <c>?drillthrough=</c> query the host resolves. Null when there's no action.</summary>
    private string? ResolveLink(ReportElement element, IReportExpressionContext ctx)
    {
        if (element.Action is not { } a)
        {
            return null;
        }
        return a.Kind switch
        {
            ActionKind.Hyperlink when !string.IsNullOrWhiteSpace(a.Hyperlink) => ResolveText(a.Hyperlink, ctx),
            ActionKind.BookmarkLink when !string.IsNullOrWhiteSpace(a.BookmarkId) => "#bm-" + a.BookmarkId,
            ActionKind.DrillthroughReport when !string.IsNullOrWhiteSpace(a.DrillthroughReportName)
                => "?drillthrough=" + Uri.EscapeDataString(a.DrillthroughReportName!),
            _ => null,
        };
    }

    /// <summary>Estimates the height of a band without producing primitives — used for KeepTogether
    /// checks and page-fit pre-checks.</summary>
    public Unit Measure(IBand band, IReportExpressionContext ctx)
    {
        // Mirror Render's height computation EXACTLY (both apply EffectiveBandHeight): the per-element bottom
        // comes from the shared EffectiveElementBottom, which resolves the SAME effective element + style as
        // RenderElement (property bindings → conditional formats → font/format). Keeping a single source of
        // truth means a binding/conditional that changes the measured height can't make Measure and Render
        // disagree — which, on the floor-less shrink path, would overlap or gap the next band.
        Unit contentExtent = Unit.Zero;
        foreach (var rawElement in band.Elements)
        {
            var bottom = EffectiveElementBottom(rawElement, ctx);
            if (bottom > contentExtent)
            {
                contentExtent = bottom;
            }
        }
        return EffectiveBandHeight(band, contentExtent);
    }

    /// <summary>Resolves each of the element's per-property expression bindings against the current
    /// row context and overlays the results onto a copy. A binding whose expression fails to evaluate
    /// is skipped (the property keeps its static value) — a bad binding never breaks the render.</summary>
    private ReportElement ApplyPropertyExpressions(ReportElement element, IReportExpressionContext ctx)
    {
        var result = element;
        foreach (var (path, expression) in element.PropertyExpressions)
        {
            object? raw;
            try
            {
                raw = _evaluator.Evaluate(expression, ctx);
            }
            catch (Exception ex) when (ex is ExpressionParseException or ExpressionEvaluationException)
            {
                continue; // graceful: a bad binding skips, keeping the static value — never breaks the render
            }
            result = PropertyPathBinder.Apply(result, path, raw, ctx.Culture);
        }
        return result;
    }

    private bool IsVisible(ReportElement element, IReportExpressionContext ctx)
    {
        if (!element.Visible)
        {
            return false;
        }
        if (string.IsNullOrEmpty(element.VisibleExpression))
        {
            return true;
        }
        return _evaluator.Evaluate<bool>(element.VisibleExpression, ctx);
    }

    private readonly ConcurrentDictionary<string, bool> _knownLiteral = new(StringComparer.Ordinal);

    private string ResolveText(string expression, IReportExpressionContext ctx, string? elementFormat = null)
    {
        // SSRS-style Format property: when the element carries a Format and the expression is a single
        // bare value — "{Fields.preco}" or a lone expression — with no inline ":format", evaluate it to a
        // typed value and apply that Format. This is why "{Fields.preco}" + Format "Moeda" now formats as
        // currency without needing the inline "{Fields.preco:C}". An inline format always wins.
        if (!string.IsNullOrEmpty(elementFormat))
        {
            var single = TemplateRenderer.TryGetSingleExpression(expression, out var inner) ? inner
                : !TemplateRenderer.HasPlaceholders(expression) && !_knownLiteral.ContainsKey(expression) ? expression
                : null;
            if (single is not null)
            {
                try
                {
                    var v = _evaluator.Evaluate(single, ctx);
                    return ValueFormatter.Format(v, elementFormat, ctx.Culture);
                }
                catch (ExpressionParseException)
                {
                    _knownLiteral[expression] = true;
                    return expression;
                }
            }
        }
        if (TemplateRenderer.HasPlaceholders(expression))
        {
            return _templates.Render(expression, ctx);
        }
        // Strings without any "{}" placeholder fall back to a literal label if they don't parse
        // as NCalc — this lets users write `.Text("Relatório de Vendas")` without quoting.
        if (_knownLiteral.TryGetValue(expression, out _))
        {
            return expression;
        }
        try
        {
            var value = _evaluator.Evaluate(expression, ctx);
            return value is null ? string.Empty : Convert.ToString(value, ctx.Culture) ?? string.Empty;
        }
        catch (ExpressionParseException)
        {
            _knownLiteral[expression] = true;
            return expression;
        }
    }

    /// <summary>Resolves a TextBox's display text. Multi-run (RDL F1.8): resolve each run's value and
    /// concatenate, drawn with the TextBox's Style (per-run style/action round-trips in the model but the
    /// mixed-font drawing path is a follow-up — single-style for now). Format's single-expression shortcut
    /// doesn't apply to a concatenation, so runs resolve without it. Empty runs → the legacy single
    /// <see cref="TextBoxElement.Expression"/> path. Used by both render and <see cref="Measure"/> so the two
    /// never diverge (a divergence would crash on / mis-measure a multi-run textbox).</summary>
    private string ResolveTextBoxText(TextBoxElement tb, IReportExpressionContext ctx, string? elementFormat)
        => tb.TextRuns.Count > 0
            ? string.Concat(tb.TextRuns.Select(r => ResolveText(r.Value, ctx)))
            : ResolveText(tb.Expression, ctx, elementFormat);

    // Publishes a named element's rendered text so ReportItems!Name.Value resolves in later-rendered bands
    // (e.g. a page footer echoing a body text box). Unnamed elements are skipped.
    private static void RecordReportItem(ReportElement element, string value, IReportExpressionContext ctx)
    {
        if (!string.IsNullOrEmpty(element.Name))
        {
            ctx.SetReportItem(element.Name, value);
        }
    }

    /// <summary>Resolves the rows a data-bound element (chart, sparkline) iterates: the named
    /// data source when <paramref name="dataSetName"/> matches a registered source, otherwise
    /// the report's primary source (falling back to the first registered source).</summary>
    private IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> ResolveRows(string? dataSetName = null)
    {
        if (!string.IsNullOrEmpty(dataSetName) && _dataSources.TryGetValue(dataSetName, out var named))
        {
            return named;
        }
        if (_primarySource is not null && _dataSources.TryGetValue(_primarySource, out var primary))
        {
            return primary;
        }
        foreach (var kv in _dataSources)
        {
            return kv.Value;
        }
        return [];
    }

    private DrawTextPrimitive EmitText(string text, Rectangle bounds, TextStyle style, string? id, bool canGrow = false, bool canShrink = false)
    {
        Rectangle actualBounds = bounds;
        if (canGrow || canShrink)
        {
            var size = _measurer.Measure(text, style, bounds.Width);
            var newHeight = bounds.Height;
            if (canGrow && size.Height > bounds.Height)
            {
                newHeight = size.Height;
            }
            else if (canShrink && size.Height < bounds.Height)
            {
                newHeight = size.Height;
            }
            actualBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, newHeight);
        }
        return new DrawTextPrimitive
        {
            Bounds = actualBounds,
            Text = text,
            Style = style,
            SourceElementId = id,
        };
    }

    private static DrawLinePrimitive EmitLine(LineElement line, Rectangle bounds)
    {
        var pen = new PenStyle(line.Pen.Color, line.Pen.Thickness, line.Pen.Style);
        var (from, to) = line.Direction switch
        {
            LineDirection.Horizontal =>
                (new Point(bounds.X, bounds.Y + bounds.Height / 2), new Point(bounds.Right, bounds.Y + bounds.Height / 2)),
            LineDirection.Vertical =>
                (new Point(bounds.X + bounds.Width / 2, bounds.Y), new Point(bounds.X + bounds.Width / 2, bounds.Bottom)),
            LineDirection.BottomLeftToTopRight =>
                (new Point(bounds.X, bounds.Bottom), new Point(bounds.Right, bounds.Y)),
            _ =>
                (new Point(bounds.X, bounds.Y), new Point(bounds.Right, bounds.Bottom)),
        };
        return new DrawLinePrimitive
        {
            Bounds = bounds,
            From = from,
            To = to,
            Pen = pen,
            SourceElementId = line.Id,
        };
    }

    private static PenStyle? ResolveBorderPen(ReportElement element)
    {
        var border = element.Style.Border;
        if (border is null)
        {
            return null;
        }
        // Element-level fallback: use the top side as representative.
        return PenStyle.FromBorderSide(border.Top);
    }

    // The effective Core style after applying any matching conditional formats. Carries Format (the
    // value-format spec), which the drawing-only TextStyle drops — so the textbox value can honour it.
    private Style ResolveEffectiveStyle(ReportElement element, IReportExpressionContext ctx)
        => StyleResolver.Resolve(element, _evaluator, ctx);

    private static TextStyle BuildTextStyle(Style style)
        => new(
            style.Font ?? Font.Default,
            style.ForeColor ?? Color.Black,
            style.HorizontalAlignment,
            style.VerticalAlignment,
            style.WordWrap,
            style.Padding ?? Thickness.Zero);

    private static Unit MaxHeight(Unit current, Rectangle bounds, Point origin, Rectangle? growsTo)
    {
        var bottomRelative = (growsTo ?? bounds).Bottom - origin.Y;
        return bottomRelative > current ? bottomRelative : current;
    }

    /// <summary>Axis-aligned intersection of two rectangles (empty when they don't overlap). Used to combine a
    /// nested container's clip with its parent's so the tighter bound wins.</summary>
    private static Rectangle IntersectRect(Rectangle a, Rectangle b)
    {
        var x = a.X > b.X ? a.X : b.X;
        var y = a.Y > b.Y ? a.Y : b.Y;
        var right = a.Right < b.Right ? a.Right : b.Right;
        var bottom = a.Bottom < b.Bottom ? a.Bottom : b.Bottom;
        return new Rectangle(x, y, right > x ? right - x : Unit.Zero, bottom > y ? bottom - y : Unit.Zero);
    }

    // A Style.BackgroundImage (phase B = External) resolves like an Image element: a per-row expression
    // (yielding bytes or a path) takes precedence, otherwise a static file/URL path.
    private EquatableArray<byte> ResolveBackgroundBytes(Reporting.Styling.BackgroundImage bg, IReportExpressionContext ctx)
        => bg.IsExpression ? ResolveExpression(bg.Expression, ctx) : LoadFile(bg.Path);

    private EquatableArray<byte> ResolveImageBytes(ImageElement image, IReportExpressionContext ctx)
    {
        return image.Source switch
        {
            ImageSourceKind.Inline => image.InlineData,
            ImageSourceKind.Path => LoadFile(image.Path),
            ImageSourceKind.Expression => ResolveExpression(image.Expression, ctx),
            _ => EquatableArray<byte>.Empty,
        };
    }

    private static EquatableArray<byte> LoadFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return EquatableArray<byte>.Empty;
        }
        return new EquatableArray<byte>(File.ReadAllBytes(path));
    }

    private EquatableArray<byte> ResolveExpression(string? expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return EquatableArray<byte>.Empty;
        }
        var value = _evaluator.Evaluate(expr, ctx);
        return value switch
        {
            byte[] bytes => new EquatableArray<byte>(bytes),
            string path when File.Exists(path) => new EquatableArray<byte>(File.ReadAllBytes(path)),
            _ => EquatableArray<byte>.Empty,
        };
    }
}

internal readonly record struct BandLayout(List<LayoutPrimitive> Primitives, Unit Height);
