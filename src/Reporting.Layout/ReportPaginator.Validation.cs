using Reporting.Geometry;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private static void ValidatePageSetup(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var setup = definition.PageSetup;
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(setup.Paper);

        // Calculate in long before using Unit's int arithmetic, including multiplication by the
        // number of gutters. Invalid dimensions must fail before opening any data source.
        long width = setup.PageWidth.Mils;
        long contentWidth = width - setup.Margins.Left.Mils - setup.Margins.Right.Mils;
        if (width <= 0 || contentWidth <= 0 || contentWidth > int.MaxValue
            || setup.Columns < 1 || setup.ColumnSpacing < Unit.Zero)
        {
            throw new ArgumentException("A página deve ter largura útil positiva, ao menos uma coluna e espaçamento não negativo.", nameof(definition));
        }

        int columns = setup.IsContinuous ? 1 : setup.Columns;
        long columnWidth = (contentWidth - (long)setup.ColumnSpacing.Mils * (columns - 1)) / columns;
        if (columnWidth <= 0)
        {
            throw new ArgumentException("As margens e o espaçamento devem deixar largura útil positiva em cada coluna.", nameof(definition));
        }

        // Zero paper height is the documented thermal-roll sentinel, not an empty finite page.
        if (!setup.IsContinuous)
        {
            long height = setup.PageHeight.Mils;
            long contentHeight = height - setup.Margins.Top.Mils - setup.Margins.Bottom.Mils;
            long bodyHeight = contentHeight - (definition.PageFooter?.Height.Mils ?? 0);
            if (height <= 0 || contentHeight <= 0 || contentHeight > int.MaxValue
                || bodyHeight <= 0 || bodyHeight > int.MaxValue)
            {
                throw new ArgumentException("As margens e o rodapé devem deixar altura útil positiva na página finita.", nameof(definition));
            }
        }
    }
}
