using System.Reflection;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Reflection-driven parity net for the <b>RDL projection</b>, the counterpart of
/// <see cref="ReflectionRoundTripTests"/> (which covers the lossless native formats).
///
/// <para>RDL is interop, not the source of truth: the native model carries concepts SSRS has no element for,
/// so a full round-trip is impossible by design. This is therefore a CHARACTERIZATION test — it asserts the
/// lossy set EXACTLY, in both directions:</para>
/// <list type="bullet">
///   <item>a property that starts being dropped (e.g. a new model field nobody wired into RdlWriter) makes it
///     FAIL — that is the whole point, and it is exactly how <c>MinColumnWidth</c> (#212) and
///     <c>ToggleItemId</c> (#217) escaped into production;</item>
///   <item>a property that starts surviving also makes it FAIL, so closing an RDL gap forces this documented
///     contract to be updated deliberately rather than drifting.</item>
/// </list>
///
/// <para><b>The lists below are a map of the RDL export debt, not a wish list.</b> Every entry is a real gap
/// measured against the live exporter; see ROADMAP.md item 12.</para>
/// </summary>
public class RdlReflectionParityTests
{
    /// <summary>Element types the RDL export drops ENTIRELY today (the re-imported report has no element).
    /// SSRS has no native counterpart for most of them; the model's own <c>CustomReportItem</c> projection is
    /// read on import but not written on export — that asymmetry is the debt.</summary>
    private static readonly HashSet<string> NotRepresentable = new()
    {
        "BarcodeElement",    // no RDL element; would need CustomReportItem on export
        "CodeElement",       // native-only (Roslyn snippet), no RDL concept
        "DataBarElement",    // RDL has <CustomReportItem> for these data-viz types…
        "IndicatorElement",  // …but the exporter does not emit them
        "SparklineElement",
        "MapElement",        // RDL <Map> is a large sub-schema, not projected
        "EllipseElement",    // RDL has no ellipse (only Rectangle/Line)
        "TableElement",      // legacy flat table; the Tablix projection supersedes it
    };

    /// <summary>Properties EVERY surviving element loses through the projection today.</summary>
    private static readonly HashSet<string> CommonLossy = new()
    {
        "Id",                   // by design: RDL identity is <c>@Name</c>, regenerated on import (omni_auto_ prefix)
        "Style",                // the style projection is partial (not every Style facet maps to RDL <Style>)
        "ConditionalFormats",   // native-only concept — SSRS expresses it as per-property expressions
        "PropertyExpressions",  // ditto: RDL binds expressions per attribute, not as a generic bag
        "VisibleExpression",    // <Visibility><Hidden> is written, but the expression form is not projected
        "ToggleItemId",         // IMPORT was fixed in #217; the WRITER still does not emit <ToggleItem>
        "InitiallyHidden",      // same gap as ToggleItemId — export side still open
    };

    /// <summary>Extra losses per element type, on top of <see cref="CommonLossy"/>.</summary>
    private static readonly Dictionary<string, string[]> ExtraLossy = new()
    {
        ["ChartElement"] = ["Title", "ShowLegend", "Series"],          // <Chart> projection carries shape only
        ["GaugeElement"] = ["Ranges"],                                  // gauge ranges not projected
        ["ImageElement"] = ["Source", "Path", "Expression", "InlineData"], // only one image source form survives
        ["LineElement"] = ["Direction", "Pen"],                         // direction is re-derived from geometry
        ["RectangleElement"] = ["FillColor", "CornerRadius", "Children"], // fill/radius/nesting not projected
        ["TablixElement"] = ["RowGroups", "ColumnGroups", "Cells", "ColumnWidths"], // reshaped by the Tablix projection
        ["TextBoxElement"] = ["Expression", "TextRuns"],                // value vs runs collapse to one form
    };

    public static TheoryData<Type> ElementTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var t in ElementPopulator.ConcreteElementTypes())
        {
            data.Add(t);
        }
        return data;
    }

    [Fact]
    public void Covers_every_concrete_element_type()
    {
        ((IEnumerable<object[]>)ElementTypes()).Count()
            .Should().BeGreaterThanOrEqualTo(17, "the RDL net must consider every concrete ReportElement subtype");
    }

    [Theory]
    [MemberData(nameof(ElementTypes))]
    public void The_rdl_projection_loses_exactly_the_documented_set(Type elementType)
    {
        var element = (ReportElement)ElementPopulator.Populate(elementType, 0);
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40), EquatableArray<ReportElement>.Empty))
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(60),
                new EquatableArray<ReportElement>(new[] { element })),
        };

        var rdl = new RdlExporter();
        var loaded = rdl.LoadFromBytes(rdl.SaveToBytes(def));
        var back = AllElements(loaded).FirstOrDefault(e => e.GetType() == elementType);

        if (NotRepresentable.Contains(elementType.Name))
        {
            back.Should().BeNull(
                $"{elementType.Name} is documented as not representable in RDL — if the exporter now emits it, " +
                "remove it from NotRepresentable and add its real lossy set to ExtraLossy");
            return;
        }

        back.Should().NotBeNull(
            $"{elementType.Name} must survive the RDL projection — if it genuinely cannot, add it to NotRepresentable");

        var lossy = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var prop in elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (ElementPopulator.Excluded.Contains(prop.Name) || prop.GetMethod is null)
            {
                continue;
            }
            if (!Equals(prop.GetValue(element), prop.GetValue(back)))
            {
                lossy.Add(prop.Name);
            }
        }

        var documented = new SortedSet<string>(CommonLossy, StringComparer.Ordinal);
        foreach (var extra in ExtraLossy.TryGetValue(elementType.Name, out var e) ? e : [])
        {
            documented.Add(extra);
        }
        documented.IntersectWith(elementType.GetProperties().Select(p => p.Name)); // ignore entries not on this type

        lossy.Should().Equal(documented,
            $"the RDL projection of {elementType.Name} must lose EXACTLY the documented set. " +
            $"Extra losses = a serializer gap (fix it, or document it here with why). " +
            $"Missing losses = a gap you just closed (delete the entry so the contract stays honest).");
    }

    private static IEnumerable<ReportElement> AllElements(ReportDefinition def)
    {
        var bands = new IEnumerable<ReportElement>?[]
        {
            def.ReportHeader?.Elements, def.PageHeader?.Elements, def.Detail.Elements,
            def.PageFooter?.Elements, def.ReportFooter?.Elements,
        };
        foreach (var band in bands)
        {
            foreach (var element in band ?? Enumerable.Empty<ReportElement>())
            {
                yield return element;
                if (element is RectangleElement rect)
                {
                    foreach (var child in rect.Children)
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
