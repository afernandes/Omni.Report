using System.Text.RegularExpressions;
using Reporting.Elements;
using Reporting.Layout.Internal;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private ReportDefinition? ResolveSubreport(string id, PaginationRequest request)
    {
        if (!_resolvedSubreports.TryGetValue(id, out var definition))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            definition = request.SubreportResolver?.Invoke(id);
            _resolvedSubreports[id] = definition;
        }
        return definition;
    }

    private IEnumerable<string> RequiredSources(PaginationRequest request)
    {
        var names = request.DataSources.Names.ToArray();
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitions = new Queue<(ReportDefinition Definition, int Depth)>();
        var seen = new HashSet<ReportDefinition>(ReferenceEqualityComparer.Instance);
        definitions.Enqueue((request.Definition, 0));
        if (ResolvePrimaryName(request) is { } primary) required.Add(primary);
        while (definitions.TryDequeue(out var entry))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(entry.Definition)) continue;
            var values = DefinitionInspection.Walk(entry.Definition, _cancellationToken).ToArray();
            var strings = values.OfType<string>().ToArray();
            foreach (var name in names)
            {
                string pattern = @"(?:Fields[!.])" + Regex.Escape(name) + @"[!.]|['" + "\"" + "]" + Regex.Escape(name) + "['\"]";
                if (strings.Any(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                {
                    required.Add(name);
                }
            }
            if (entry.Depth > 0 && ResolvePrimaryName(new PaginationRequest { Definition = entry.Definition, DataSources = request.DataSources }) is { } childPrimary)
                required.Add(childPrimary);
            if (entry.Depth >= MaxSubreportDepth) continue;
            foreach (var sub in values.OfType<SubreportElement>())
            {
                var child = sub.InlineDefinition ?? (sub.ReportId is { Length: > 0 } id ? ResolveSubreport(id, request) : null);
                if (child is not null) definitions.Enqueue((child, entry.Depth + 1));
            }
        }
        return names.Where(required.Contains);
    }
}
