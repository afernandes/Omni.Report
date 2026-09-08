using Reporting.Bands;
using Reporting.Common;
using Reporting.Layout.Internal;
namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private EquatableArray<PaginationDiagnostic> InspectLimitations(ReportDefinition definition)
    {
        var diagnostics = new List<PaginationDiagnostic>();
        foreach (var group in new[] { definition }.Concat(_resolvedSubreports.Values.OfType<ReportDefinition>()).SelectMany(item => DefinitionInspection.Walk(item, _cancellationToken)).OfType<GroupBand>().Distinct())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(group.FilterExpression)) Add(group, "FilterExpression");
            if (group.SortExpressions.Count > 0) Add(group, "SortExpressions");
            if (group.KeepTogether) Add(group, "KeepTogether");
        }
        return new(diagnostics);
        void Add(GroupBand group, string feature) => diagnostics.Add(new("ORL024", $"Group[{group.Name}].{feature}",
            $"O grupo '{group.Name}' declara {feature}, que não é integralmente suportado pelo paginador. A definição precisa ser ajustada antes de usar este resultado em produção."));
    }
}
