using Reporting.Common;
using Reporting.Elements;
using Reporting.Layout.Internal;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    // Code for Tablix content that the model accepts and persists but the renderer cannot draw. The flat Tablix has
    // exactly two template rows (0 = header, 1 = repeated detail) and the matrix reads only the corner and the first
    // body cell, so multi-row static headers — the only place a RowSpan means anything — do not exist yet, nor do
    // several measures per matrix intersection. Until they land, the loss is reported instead of silently producing a
    // grid that differs from the definition.
    internal const string UnsupportedTablixRowsCode = "ORL025";

    /// <summary>Walks the definition and every subreport already resolved for this run, reporting content that
    /// would otherwise be dropped from the output without notice.</summary>
    private EquatableArray<PaginationDiagnostic> InspectLimitations(ReportDefinition definition)
    {
        var diagnostics = new List<PaginationDiagnostic>();
        var definitions = new[] { definition }.Concat(_resolvedSubreports.Values.OfType<ReportDefinition>());
        foreach (var tablix in definitions.SelectMany(item => DefinitionInspection.Walk(item, _cancellationToken))
                     .OfType<TablixElement>().Distinct())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            string name = string.IsNullOrWhiteSpace(tablix.Name) ? tablix.Id : tablix.Name;
            if (tablix.Cells.Any(cell => cell.RowSpan > 1))
            {
                diagnostics.Add(new(UnsupportedTablixRowsCode, $"Tablix[{name}].RowSpan",
                    $"O Tablix '{name}' declara RowSpan maior que 1, que o renderizador ainda não mescla: cada " +
                    "linha-modelo é desenhada separadamente. Use ColumnSpan ou remova o RowSpan."));
            }

            bool isMatrix = tablix.RowGroups.Count >= 1 && tablix.ColumnGroups.Count >= 1;
            if (!isMatrix && tablix.Cells.Any(cell => cell.RowIndex >= 2))
            {
                diagnostics.Add(new(UnsupportedTablixRowsCode, $"Tablix[{name}].TemplateRows",
                    $"O Tablix '{name}' tem células além da linha 1. A tabela usa só a linha 0 (cabeçalho) e a " +
                    "linha 1 (detalhe repetido); as demais não são desenhadas."));
            }

            int ignored = isMatrix ? CountMatrixCellsNotDrawn(tablix) : 0;
            if (ignored > 0)
            {
                diagnostics.Add(new(UnsupportedTablixRowsCode, $"Tablix[{name}].MatrixCells",
                    $"A matriz '{name}' tem {ignored} {(ignored == 1 ? "célula" : "células")} que não " +
                    $"{(ignored == 1 ? "é desenhada" : "são desenhadas")}: ela usa só o canto (linha 0, coluna 0) e " +
                    "a primeira caixa de texto do corpo — os cabeçalhos vêm dos valores dos grupos, e uma segunda " +
                    "medida não é suportada."));
            }
        }
        return new(diagnostics);
    }

    // Mirrors TablixRenderer.RenderMatrixCore: it draws the corner (0, 0) only as a LabelElement's text, and the FIRST
    // TextBoxElement whose row and column are both >= 1; every other cell with content is dropped.
    private static int CountMatrixCellsNotDrawn(TablixElement tablix)
    {
        bool bodyTaken = false;
        int ignored = 0;
        foreach (var cell in tablix.Cells)
        {
            if (cell.Content is null || (cell.RowIndex == 0 && cell.ColumnIndex == 0 && cell.Content is LabelElement))
            {
                continue;
            }
            if (!bodyTaken && cell.RowIndex >= 1 && cell.ColumnIndex >= 1 && cell.Content is TextBoxElement)
            {
                bodyTaken = true;
                continue;
            }
            ignored++;
        }
        return ignored;
    }
}
