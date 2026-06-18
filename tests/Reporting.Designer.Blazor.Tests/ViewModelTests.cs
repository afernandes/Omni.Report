using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

public class ViewModelTests
{
    [Fact]
    public void Element_kind_round_trips_through_to_from_element()
    {
        var vm = new ElementViewModel(DesignerElementKind.TextBox, "abc")
        {
            Expression = "{Fields.Total:C}",
            X = Unit.FromMm(10),
            Y = Unit.FromMm(20),
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(6),
            FontSize = 12,
            IsBold = true,
        };
        var element = vm.ToElement();
        var back = ElementViewModel.FromElement(element);
        back.Kind.Should().Be(DesignerElementKind.TextBox);
        back.Expression.Should().Be("{Fields.Total:C}");
        back.FontSize.Should().Be(12);
        back.IsBold.Should().BeTrue();
        back.X.Should().Be(vm.X);
    }

    [Theory]
    [InlineData(DesignerElementKind.Label)]
    [InlineData(DesignerElementKind.TextBox)]
    [InlineData(DesignerElementKind.Line)]
    [InlineData(DesignerElementKind.Rectangle)]
    [InlineData(DesignerElementKind.Ellipse)]
    [InlineData(DesignerElementKind.Image)]
    [InlineData(DesignerElementKind.Barcode)]
    public void Every_element_kind_materializes_an_element(DesignerElementKind kind)
    {
        var vm = new ElementViewModel(kind, "id");
        var element = vm.ToElement();
        element.Should().NotBeNull();
        element.Id.Should().Be("id");
    }

    [Fact]
    public void Definition_view_model_builds_definition_with_seeded_bands()
    {
        var vm = new ReportDefinitionViewModel("Test");
        vm.Bands.Should().HaveCount(3);
        vm.FindBand(DesignerBandKind.Detail).Should().NotBeNull();
        var def = vm.Build();
        def.Name.Should().Be("Test");
        def.PageHeader.Should().NotBeNull();
        def.PageFooter.Should().NotBeNull();
    }

    [Fact]
    public void Designer_state_round_trip_via_repx()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.Label, "lbl1")
        {
            Text = "Hello",
            X = Unit.FromMm(10),
            Y = Unit.FromMm(1),
            Width = Unit.FromMm(50),
            Height = Unit.FromMm(6),
        });
        var bytes = state.Save();
        bytes.Should().NotBeEmpty();
        state.IsDirty.Should().BeFalse();

        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        var reloadedDetail = reloaded.Report.FindBand(DesignerBandKind.Detail)!;
        reloadedDetail.Elements.Should().ContainSingle();
        reloadedDetail.Elements[0].Text.Should().Be("Hello");
    }

    [Fact]
    public void Designer_state_marks_dirty_on_element_change()
    {
        var state = new DesignerState();
        state.IsDirty.Should().BeFalse();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "x"));
        state.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Selection_change_clears_previous_selection()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var a = new ElementViewModel(DesignerElementKind.Label, "a") { Text = "A" };
        var b = new ElementViewModel(DesignerElementKind.Label, "b") { Text = "B" };
        detail.AddElement(a);
        detail.AddElement(b);

        state.SelectedElement = a;
        a.IsSelected.Should().BeTrue();
        state.SelectedElement = b;
        a.IsSelected.Should().BeFalse();
        b.IsSelected.Should().BeTrue();
        state.SelectedElement = null;
        b.IsSelected.Should().BeFalse();
    }
}

public class CommandTests
{
    [Fact]
    public void Add_command_appends_element_undo_removes()
    {
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(8));
        var element = new ElementViewModel(DesignerElementKind.Label, "x") { Text = "X" };
        var history = new CommandHistory();
        history.Push(new AddElementCommand(band, element));
        band.Elements.Should().ContainSingle();
        history.CanUndo.Should().BeTrue();
        history.Undo();
        band.Elements.Should().BeEmpty();
        history.CanRedo.Should().BeTrue();
        history.Redo();
        band.Elements.Should().ContainSingle();
    }

    [Fact]
    public void Move_command_changes_position_and_undoes()
    {
        var element = new ElementViewModel(DesignerElementKind.Label, "x")
        {
            X = Unit.FromMm(10), Y = Unit.FromMm(20),
        };
        var history = new CommandHistory();
        history.Push(new MoveElementCommand(element, Unit.FromMm(50), Unit.FromMm(60)));
        element.X.ToMm().Should().BeApproximately(50, 0.1);
        element.Y.ToMm().Should().BeApproximately(60, 0.1);
        history.Undo();
        element.X.ToMm().Should().BeApproximately(10, 0.1);
        element.Y.ToMm().Should().BeApproximately(20, 0.1);
    }

    [Fact]
    public void Resize_command_changes_size_and_undoes()
    {
        var element = new ElementViewModel(DesignerElementKind.Label, "x")
        {
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
        };
        var history = new CommandHistory();
        history.Push(new ResizeElementCommand(element, Unit.FromMm(80), Unit.FromMm(12)));
        element.Width.ToMm().Should().BeApproximately(80, 0.1);
        element.Height.ToMm().Should().BeApproximately(12, 0.1);
        history.Undo();
        element.Width.ToMm().Should().BeApproximately(40, 0.1);
    }

    [Fact]
    public void Change_property_command_round_trips()
    {
        var element = new ElementViewModel(DesignerElementKind.TextBox, "x") { Expression = "old" };
        var history = new CommandHistory();
        history.Push(new ChangePropertyCommand<string>("Edit expression",
            () => element.Expression, v => element.Expression = v, "new"));
        element.Expression.Should().Be("new");
        history.Undo();
        element.Expression.Should().Be("old");
        history.Redo();
        element.Expression.Should().Be("new");
    }

    [Fact]
    public void History_clears_redo_on_new_push()
    {
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(8));
        var history = new CommandHistory();
        history.Push(new AddElementCommand(band, new ElementViewModel(DesignerElementKind.Label, "a")));
        history.Undo();
        history.CanRedo.Should().BeTrue();
        history.Push(new AddElementCommand(band, new ElementViewModel(DesignerElementKind.Label, "b")));
        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void History_respects_limit()
    {
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(8));
        var history = new CommandHistory { Limit = 5 };
        for (int i = 0; i < 10; i++)
        {
            history.Push(new AddElementCommand(band, new ElementViewModel(DesignerElementKind.Label, $"id{i}")));
        }
        int undos = 0;
        while (history.Undo()) undos++;
        undos.Should().Be(5);
    }
}
