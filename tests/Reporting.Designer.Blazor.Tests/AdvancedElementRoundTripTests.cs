using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Covers the "opaque advanced element" round-trip: Tablix, Gauge, DataBar, Sparkline, Indicator
/// and Map have no dedicated designer editor yet, but loading and re-saving them must NOT degrade
/// them to a TextBox — the designer preserves the full domain element and only updates its bounds.
/// </summary>
public class AdvancedElementRoundTripTests
{
    [Fact]
    public void TextBox_TextRuns_survive_a_designer_edit_and_clone()
    {
        // TextBox is a first-class editor (not opaque). Without the run-mirror, opening a multi-run
        // textbox and editing any property would silently drop its TextRuns — a data-loss bug.
        var tb = new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(6)),
            Expression = "Olá {Fields.nome}",
            TextRuns = EquatableArray.Create(
                new TextRun("Olá "),
                new TextRun("Fields.nome", new Reporting.Styling.Style { Font = new Reporting.Styling.Font("Arial", 10, Reporting.Styling.FontStyle.Bold) })),
        };

        var vm = ElementViewModel.FromElement(tb);
        vm.Kind.Should().Be(DesignerElementKind.TextBox);
        vm.X = Unit.FromMm(15); // edit a property

        var back = (TextBoxElement)vm.ToElement();
        back.TextRuns.Should().HaveCount(2, "runs survive an edit");
        back.TextRuns[1].Value.Should().Be("Fields.nome");
        back.TextRuns[1].Style!.Font!.Style.Should().HaveFlag(Reporting.Styling.FontStyle.Bold);
        back.Bounds.X.Should().Be(Unit.FromMm(15));

        var clone = (TextBoxElement)vm.Clone().ToElement();
        clone.TextRuns.Should().HaveCount(2, "Clone deep-copies the runs");
    }

    [Fact]
    public void Style_BackgroundImage_survives_a_designer_edit_and_clone()
    {
        // No dedicated editor yet — but editing any unrelated property goes through ToElement, which must
        // NOT silently drop the background image (data-loss regression caught by adversarial review).
        var lbl = new LabelElement
        {
            Id = "l",
            Text = "fundo",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(20)),
            Style = new Reporting.Styling.Style(BackgroundImage: new Reporting.Styling.BackgroundImage(Path: "logo.png")),
        };

        var vm = ElementViewModel.FromElement(lbl);
        vm.X = Unit.FromMm(10); // edit an unrelated property
        var back = vm.ToElement();
        back.Style.BackgroundImage.Should().NotBeNull();
        back.Style.BackgroundImage!.Path.Should().Be("logo.png", "the bg image survives an unrelated edit");

        var clone = vm.Clone().ToElement();
        clone.Style.BackgroundImage!.Path.Should().Be("logo.png", "Clone preserves the bg image");
    }

    [Fact]
    public void Gauge_round_trips_losslessly_and_stays_movable()
    {
        var gauge = new GaugeElement
        {
            Id = "g1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(40)),
            Kind = GaugeKind.Radial,
            ValueExpression = "Sum(Fields.total)",
            MinimumExpression = "0",
            MaximumExpression = "1000",
            Ranges = new EquatableArray<GaugeRange>([new GaugeRange("0", "500", "#EF4444")]),
        };

        var vm = ElementViewModel.FromElement(gauge);
        vm.Kind.Should().Be(DesignerElementKind.Gauge);

        vm.X = Unit.FromMm(10); // move it in the designer

        var back = vm.ToElement();
        back.Should().BeOfType<GaugeElement>();
        var g = (GaugeElement)back;
        g.ValueExpression.Should().Be("Sum(Fields.total)");
        g.MaximumExpression.Should().Be("1000");
        g.Ranges.Should().HaveCount(1);
        g.Bounds.X.Should().Be(Unit.FromMm(10));
    }

    [Fact]
    public void Editing_an_opaque_elements_metadata_preserves_its_inherited_style()
    {
        // A Gauge with the default Style has Font/ForeColor = null (inherit). Opaque elements have NO
        // appearance editor, so a metadata edit must not materialise Arial/10/Black over the inherited look.
        var gauge = new GaugeElement
        {
            Id = "g1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(40)),
            MaximumExpression = "100",
        };
        gauge.Style.Font.Should().BeNull("precondition: inherited (null) font");
        gauge.Style.ForeColor.Should().BeNull("precondition: inherited fore colour");

        var vm = ElementViewModel.FromElement(gauge);
        var maxDesc = PropertyGridDescriptors.For(typeof(GaugeElement)).Single(d => d.Name == "MaximumExpression");
        vm.ApplyMetaSet(maxDesc, "200");

        var back = (GaugeElement)vm.ToElement();
        back.MaximumExpression.Should().Be("200");
        back.Style.Font.Should().BeNull("the inherited font must survive a metadata edit on an opaque element");
        back.Style.ForeColor.Should().BeNull("the inherited fore colour must survive");
    }

    [Fact]
    public void Tablix_is_not_degraded_to_a_textbox_on_round_trip()
    {
        var tablix = new TablixElement
        {
            Id = "t1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(120), Unit.FromMm(30)),
            DataSetName = "Vendas",
            Cells = new EquatableArray<TablixCell>(
            [
                new TablixCell(0, 0, new LabelElement { Text = "Produto", Bounds = Rectangle.Empty }),
                new TablixCell(1, 0, new TextBoxElement { Expression = "Fields.nome", Bounds = Rectangle.Empty }),
            ]),
        };

        var back = ElementViewModel.FromElement(tablix).ToElement();

        back.Should().BeOfType<TablixElement>();
        var t = (TablixElement)back;
        t.DataSetName.Should().Be("Vendas");
        t.Cells.Should().HaveCount(2);
    }

    [Fact]
    public void Freshly_added_advanced_element_materialises_to_its_own_type()
    {
        // No source element (added from the toolbox) → ToElement must still produce the right type.
        var vm = new ElementViewModel(DesignerElementKind.Map, "m1")
        {
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(60),
        };

        vm.ToElement().Should().BeOfType<MapElement>();
    }

    [Fact]
    public void Map_every_field_is_editable_on_a_freshly_added_element()
    {
        // User adds a Map from the toolbox (no _sourceElement) and configures EVERY field in the
        // PropertyGrid — each setter must seed/mutate the domain element so ToElement() carries it.
        // This is the "build + edit any parameter in the designer, not only when loading a file" path.
        var vm = new ElementViewModel(DesignerElementKind.Map, "m1")
        {
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(60),
        };

        vm.MapLatitude = "Fields.lat";
        vm.MapLongitude = "Fields.lon";
        vm.MapDataSet = "Filiais";
        vm.MapShapeSet = "brazil";
        vm.MapShapesGeoJson = "{\"type\":\"FeatureCollection\"}";
        vm.MapGraticule = true;
        vm.MapShapeFill = "#FFEEDD";
        vm.MapShapeStroke = "#112233";
        vm.MapBasemap = "OpenStreetMap";

        var map = vm.ToElement().Should().BeOfType<MapElement>().Subject;
        map.LatitudeExpression.Should().Be("Fields.lat");
        map.LongitudeExpression.Should().Be("Fields.lon");
        map.DataSetName.Should().Be("Filiais");
        map.ShapeSet.Should().Be("brazil");
        map.ShapesGeoJson.Should().Be("{\"type\":\"FeatureCollection\"}");
        map.ShowGraticule.Should().BeTrue();
        map.ShapeFill.Should().Be("#FFEEDD");
        map.ShapeStroke.Should().Be("#112233");
        map.Basemap.Should().Be("OpenStreetMap");
        map.Bounds.Width.Should().Be(Unit.FromMm(80));
    }

    [Fact]
    public void Map_loaded_element_surfaces_all_fields_and_edits_stick()
    {
        var map = new MapElement
        {
            Id = "m2",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(60)),
            LatitudeExpression = "Fields.lat",
            LongitudeExpression = "Fields.lon",
            ShapeSet = "south-america",
            ShowGraticule = true,
            Basemap = "OpenStreetMap",
        };

        var vm = ElementViewModel.FromElement(map);
        // Every field is surfaced for the PropertyGrid (not just preserved opaquely).
        vm.MapShapeSet.Should().Be("south-america");
        vm.MapGraticule.Should().BeTrue();
        vm.MapBasemap.Should().Be("OpenStreetMap");

        // Edit one in the designer, re-emit — the edit sticks and the rest is preserved.
        vm.MapShapeSet = "brazil";
        var back = (MapElement)vm.ToElement();
        back.ShapeSet.Should().Be("brazil");
        back.ShowGraticule.Should().BeTrue();
        back.LatitudeExpression.Should().Be("Fields.lat");
    }

    [Fact]
    public void Code_is_editable_on_a_freshly_added_element()
    {
        // Code has no engine render (it declares Code.Method(...) helpers), but it MUST be
        // constructible + editable in the designer — not only preserved when loading a .repx.
        var vm = new ElementViewModel(DesignerElementKind.Code, "c1")
        {
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(40),
        };

        vm.CodeSource = "public static decimal Imposto(decimal v) => v * 0.18m;";
        vm.CodeLang = CodeLanguage.VisualBasic;

        var code = vm.ToElement().Should().BeOfType<CodeElement>().Subject;
        code.Source.Should().Contain("Imposto");
        code.Language.Should().Be(CodeLanguage.VisualBasic);
    }

    [Fact]
    public void Code_loaded_element_surfaces_fields_and_edits_stick()
    {
        var code = new CodeElement
        {
            Id = "c2",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(40)),
            Source = "public static int Dobro(int x) => x * 2;",
            Language = CodeLanguage.CSharp,
        };

        var vm = ElementViewModel.FromElement(code);
        vm.Kind.Should().Be(DesignerElementKind.Code);
        vm.CodeSource.Should().Contain("Dobro");

        vm.CodeLang = CodeLanguage.VisualBasic;
        var back = (CodeElement)vm.ToElement();
        back.Language.Should().Be(CodeLanguage.VisualBasic);
        back.Source.Should().Contain("Dobro");
    }

    [Fact]
    public void Subreport_is_editable_on_a_freshly_added_element()
    {
        var vm = new ElementViewModel(DesignerElementKind.Subreport, "s1")
        {
            Width = Unit.FromMm(120),
            Height = Unit.FromMm(60),
        };

        vm.SubreportReportId = "DetalhePedido";
        vm.SubreportDataExpression = "Fields.Itens";
        vm.SubreportParametersText = "pedidoId=Fields.id\ncliente=Parameters.Cliente";

        var sub = vm.ToElement().Should().BeOfType<SubreportElement>().Subject;
        sub.ReportId.Should().Be("DetalhePedido");
        sub.DataExpression.Should().Be("Fields.Itens");
        sub.ParameterBindings.Should().HaveCount(2);
        sub.ParameterBindings["pedidoId"].Should().Be("Fields.id");
        sub.ParameterBindings["cliente"].Should().Be("Parameters.Cliente");
    }

    [Fact]
    public void Subreport_round_trips_and_surfaces_parameter_bindings_as_text()
    {
        var sub = new SubreportElement
        {
            Id = "s2",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(120), Unit.FromMm(60)),
            ReportId = "Detalhe",
            DataExpression = "Fields.Linhas",
            ParameterBindings = new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["a"] = "Fields.x" }),
        };

        var vm = ElementViewModel.FromElement(sub);
        vm.Kind.Should().Be(DesignerElementKind.Subreport);
        vm.SubreportReportId.Should().Be("Detalhe");
        vm.SubreportParametersText.Should().Contain("a=Fields.x");

        // Edit the bindings as text in the designer; they re-materialise into the dictionary.
        vm.SubreportParametersText = "a=Fields.y\nb=Fields.z";
        var back = (SubreportElement)vm.ToElement();
        back.ParameterBindings.Should().HaveCount(2);
        back.ParameterBindings["a"].Should().Be("Fields.y");
        back.ParameterBindings["b"].Should().Be("Fields.z");
    }

    [Fact]
    public void Tablix_matrix_is_buildable_and_editable_in_the_designer()
    {
        // Build a crosstab from scratch in the designer (no source element), then edit every field.
        var vm = new ElementViewModel(DesignerElementKind.Tablix, "t1")
        {
            Width = Unit.FromMm(150),
            Height = Unit.FromMm(40),
        };
        vm.TablixIsMatrix.Should().BeFalse("a fresh Tablix starts as a flat table");

        vm.SetTablixMatrix(true);
        vm.TablixIsMatrix.Should().BeTrue();
        vm.TablixRowGroup = "Fields.Regiao";
        vm.TablixColumnGroup = "Fields.Mes";
        vm.TablixCorner = "Região";
        vm.TablixCellExpr = "Fields.Total";

        var t = vm.ToElement().Should().BeOfType<TablixElement>().Subject;
        t.RowGroups.Should().ContainSingle().Which.GroupExpression.Should().Be("Fields.Regiao");
        t.ColumnGroups.Should().ContainSingle().Which.GroupExpression.Should().Be("Fields.Mes");
        t.Cells.Should().Contain(c => c.RowIndex == 0 && c.ColumnIndex == 0);
        t.Cells.Should().Contain(c => c.RowIndex == 1 && c.ColumnIndex == 1);

        // Reloading the element surfaces matrix mode + the fields for further editing.
        var reloaded = ElementViewModel.FromElement(t);
        reloaded.TablixIsMatrix.Should().BeTrue();
        reloaded.TablixRowGroup.Should().Be("Fields.Regiao");
        reloaded.TablixColumnGroup.Should().Be("Fields.Mes");
        reloaded.TablixCorner.Should().Be("Região");
        reloaded.TablixCellExpr.Should().Be("Fields.Total");
    }

    [Fact]
    public void Tablix_matrix_nested_groups_round_trip_via_the_multiline_editor()
    {
        var vm = new ElementViewModel(DesignerElementKind.Tablix, "t2")
        {
            Width = Unit.FromMm(170),
            Height = Unit.FromMm(50),
        };
        vm.SetTablixMatrix(true);

        // Two nested levels on each axis — one expression per line, outer→inner.
        vm.TablixRowGroupsText = "Fields.Regiao\nFields.Cidade";
        vm.TablixColumnGroupsText = "Fields.Ano\nFields.Mes";
        vm.TablixCellExpr = "Fields.Total";

        var t = vm.ToElement().Should().BeOfType<TablixElement>().Subject;
        t.RowGroups.Select(g => g.GroupExpression).Should().Equal("Fields.Regiao", "Fields.Cidade");
        t.ColumnGroups.Select(g => g.GroupExpression).Should().Equal("Fields.Ano", "Fields.Mes");

        // Reloaded, the multi-line editor surfaces every level for further editing in the designer.
        var reloaded = ElementViewModel.FromElement(t);
        reloaded.TablixRowGroupsText.Should().Be("Fields.Regiao\nFields.Cidade");
        reloaded.TablixColumnGroupsText.Should().Be("Fields.Ano\nFields.Mes");
    }

    [Fact]
    public void Tablix_group_sort_is_editable_and_text_editor_preserves_it()
    {
        var tablix = new TablixElement
        {
            Id = "t1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(150), Unit.FromMm(40)),
            RowGroups = new EquatableArray<TablixGroup>([new TablixGroup("Rows0", "Fields.Regiao")]),
            ColumnGroups = new EquatableArray<TablixGroup>([new TablixGroup("Cols0", "Fields.Mes")]),
            Cells = new EquatableArray<TablixCell>(
                [new TablixCell(1, 1, new TextBoxElement { Expression = "Fields.Total", Bounds = Rectangle.Empty })]),
        };
        var vm = ElementViewModel.FromElement(tablix);

        vm.TablixRowGroupSort = "Fields.Total";
        vm.TablixRowGroupSortDescending = true;

        var back = (TablixElement)vm.ToElement();
        back.RowGroups[0].SortExpression.Should().Be("Fields.Total");
        back.RowGroups[0].SortDescending.Should().BeTrue();

        // Editing the group expressions via the multi-line editor must NOT wipe the configured sort.
        vm.TablixRowGroupsText = "Fields.Regiao";
        var back2 = (TablixElement)vm.ToElement();
        back2.RowGroups[0].SortExpression.Should().Be("Fields.Total", "the text editor preserves the sort by index");
        back2.RowGroups[0].SortDescending.Should().BeTrue();
    }

    // ── Container Rectangle children (PR1: visíveis + selecionáveis + editáveis) ──────

    private static RectangleElement RectWith(params ReportElement[] children) => new()
    {
        Id = "rect",
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(60)),
        Children = new EquatableArray<ReportElement>(children),
    };

    [Fact]
    public void Rectangle_children_materialise_into_editable_child_view_models()
    {
        var rect = RectWith(
            new LabelElement { Id = "c1", Text = "Título", Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(5), Unit.FromMm(40), Unit.FromMm(6)) },
            new TextBoxElement { Id = "c2", Expression = "Fields.total", Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(15), Unit.FromMm(40), Unit.FromMm(6)) });

        var vm = ElementViewModel.FromElement(rect);
        vm.Kind.Should().Be(DesignerElementKind.Rectangle);
        vm.Children.Should().HaveCount(2, "children are materialised as real child VMs, not an opaque blob");
        vm.Children[0].Kind.Should().Be(DesignerElementKind.Label);
        vm.Children[1].Kind.Should().Be(DesignerElementKind.TextBox);
        vm.Children[0].ParentElement.Should().BeSameAs(vm, "each child back-references its container");

        var back = (RectangleElement)vm.ToElement();
        back.Children.Should().HaveCount(2);
        ((LabelElement)back.Children[0]).Text.Should().Be("Título");
        ((TextBoxElement)back.Children[1]).Expression.Should().Be("Fields.total");
    }

    [Fact]
    public void Editing_a_childs_bounds_sticks_through_round_trip()
    {
        var rect = RectWith(new LabelElement { Id = "c1", Text = "x", Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(5), Unit.FromMm(40), Unit.FromMm(6)) });
        var vm = ElementViewModel.FromElement(rect);

        // Child coordinates are relative to the parent Rectangle — editing them via the child VM must persist.
        vm.Children[0].X = Unit.FromMm(12);
        vm.Children[0].Text = "editado";

        var back = (RectangleElement)vm.ToElement();
        var child = (LabelElement)back.Children[0];
        child.Bounds.X.Should().Be(Unit.FromMm(12));
        child.Text.Should().Be("editado");
    }

    [Fact]
    public void Rectangle_with_an_opaque_Tablix_child_does_not_degrade_it_to_a_textbox()
    {
        // The critical regression: materialising children into VMs must reuse the opaque-preservation path,
        // otherwise a nested Tablix (no dedicated editor) would silently become a TextBox on save.
        var rect = RectWith(new TablixElement
        {
            Id = "t",
            Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(5), Unit.FromMm(80), Unit.FromMm(30)),
            DataSetName = "Vendas",
            Cells = new EquatableArray<TablixCell>(
                [new TablixCell(0, 0, new LabelElement { Text = "P", Bounds = Rectangle.Empty })]),
        });

        var vm = ElementViewModel.FromElement(rect);
        vm.Children.Should().ContainSingle().Which.Kind.Should().Be(DesignerElementKind.Tablix);

        vm.X = Unit.FromMm(10); // edit the parent — round-trips the children
        var back = (RectangleElement)vm.ToElement();
        back.Children.Should().ContainSingle();
        back.Children[0].Should().BeOfType<TablixElement>("a nested opaque element must NOT degrade to TextBox");
        ((TablixElement)back.Children[0]).DataSetName.Should().Be("Vendas");
    }

    [Fact]
    public void Nested_rectangle_in_rectangle_round_trips_to_arbitrary_depth()
    {
        var inner = RectWith(new LabelElement { Id = "deep", Text = "fundo", Bounds = Rectangle.Empty });
        var outer = RectWith(inner with { Id = "inner" });

        var vm = ElementViewModel.FromElement(outer);
        vm.Children.Should().ContainSingle().Which.Kind.Should().Be(DesignerElementKind.Rectangle);
        vm.Children[0].Children.Should().ContainSingle("the grandchild is materialised recursively");

        var back = (RectangleElement)vm.ToElement();
        var innerBack = back.Children.Should().ContainSingle().Subject.Should().BeOfType<RectangleElement>().Subject;
        ((LabelElement)innerBack.Children.Single()).Text.Should().Be("fundo");
    }

    [Fact]
    public void Clone_deep_copies_children_with_fresh_ids()
    {
        var rect = RectWith(new LabelElement { Id = "c1", Text = "orig", Bounds = Rectangle.Empty });
        var vm = ElementViewModel.FromElement(rect);

        var clone = vm.Clone();
        clone.Children.Should().ContainSingle();
        clone.Children[0].Id.Should().NotBe("c1", "clone gives children fresh ids");
        clone.Children[0].ParentElement.Should().BeSameAs(clone);
        ((LabelElement)((RectangleElement)clone.ToElement()).Children[0]).Text.Should().Be("orig");
    }

    [Fact]
    public void Tablix_row_subtotals_are_editable()
    {
        var tablix = new TablixElement
        {
            Id = "t1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(150), Unit.FromMm(40)),
            RowGroups = new EquatableArray<TablixGroup>([new TablixGroup("Rows0", "Fields.Regiao")]),
            ColumnGroups = new EquatableArray<TablixGroup>([new TablixGroup("Cols0", "Fields.Mes")]),
            Cells = new EquatableArray<TablixCell>(
                [new TablixCell(1, 1, new TextBoxElement { Expression = "Fields.Total", Bounds = Rectangle.Empty })]),
        };
        var vm = ElementViewModel.FromElement(tablix);
        vm.TablixRowSubtotals.Should().BeFalse();

        vm.TablixRowSubtotals = true;
        ((TablixElement)vm.ToElement()).RowSubtotals.Should().BeTrue();
        ElementViewModel.FromElement((TablixElement)vm.ToElement()).TablixRowSubtotals.Should().BeTrue();
    }
}
