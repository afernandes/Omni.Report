namespace Reporting.DataSources;

/// <summary>Immutable schema backed by a flat field list.</summary>
public sealed class ReportRecordSchema : IReportRecordSchema
{
    private readonly Dictionary<string, int> _index;

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

    public IReadOnlyList<ReportField> Fields { get; }

    public int IndexOf(string name)
        => _index.TryGetValue(name, out var ordinal) ? ordinal : -1;
}
