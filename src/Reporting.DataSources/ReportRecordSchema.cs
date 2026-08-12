namespace Reporting.DataSources;

/// <summary>Immutable schema backed by a flat field list.</summary>
public sealed class ReportRecordSchema : IReportRecordSchema
{
    private readonly Dictionary<string, int> _index;

    /// <summary>Builds a schema from an ordered field list. Ordinals follow the order given.</summary>
    public ReportRecordSchema(IEnumerable<ReportField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = fields.ToArray();
        _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Fields.Count; i++)
        {
            _index[Fields[i].Name] = i;
        }
    }

    /// <summary>The fields, in ordinal order.</summary>
    public IReadOnlyList<ReportField> Fields { get; }

    /// <summary>Ordinal of <paramref name="name"/>, or <c>-1</c> when absent. Returning -1 rather than
    /// throwing is what lets an expression reference a missing field and yield null.</summary>
    public int IndexOf(string name)
        => _index.TryGetValue(name, out var ordinal) ? ordinal : -1;
}
