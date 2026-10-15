using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Undo and redo must not leave the selection pointing at an element that is no longer in the document — the
/// property grid would keep showing (and editing) a detached view model.
/// </summary>
public sealed class SelectionHistoryTests
{
    [Fact]
    public void Undo_InsercaoDoElementoSelecionado_LimpaSelecao()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var inserted = new ElementViewModel(DesignerElementKind.Barcode, "codigo");
        state.History.Push(new AddElementCommand(band, inserted));
        state.SelectedElement = inserted;

        state.History.Undo();

        state.SelectedElement.Should().BeNull();
        state.SelectedElements.Should().BeEmpty();
        inserted.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void Undo_InsercaoDeUmDosSelecionados_MantemOsQueContinuamNoDocumento()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var existing = new ElementViewModel(DesignerElementKind.Label, "existente");
        band.Elements.Add(existing);
        var inserted = new ElementViewModel(DesignerElementKind.Label, "novo");
        state.History.Push(new AddElementCommand(band, inserted));
        state.SelectMany([existing, inserted]);

        state.History.Undo();

        state.SelectedElements.Should().ContainSingle().Which.Should().BeSameAs(existing);
        state.SelectedElement.Should().BeSameAs(existing);
    }

    [Fact]
    public void Undo_InsercaoDeContainer_TiraDaSelecaoOFilhoDele()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var container = new ElementViewModel(DesignerElementKind.Rectangle, "caixa");
        var child = new ElementViewModel(DesignerElementKind.Label, "filho");
        container.AttachChild(child);
        state.History.Push(new AddElementCommand(band, container));
        state.SelectedElement = child;

        state.History.Undo();

        state.SelectedElement.Should().BeNull("the child left the document together with its container");
    }

    [Fact]
    public void Redo_ExclusaoDoElementoSelecionado_LimpaSelecao()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var element = new ElementViewModel(DesignerElementKind.Label, "rotulo");
        band.Elements.Add(element);
        state.History.Push(new RemoveElementCommand(band, element));
        state.History.Undo();
        state.SelectedElement = element;

        state.History.Redo();

        state.SelectedElement.Should().BeNull();
    }
}
