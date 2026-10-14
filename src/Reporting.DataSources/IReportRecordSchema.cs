namespace Reporting.DataSources;

/// <summary>Static schema for a tabular data source.</summary>
public interface IReportRecordSchema
{
    /// <summary>Field ordinals in iteration order.</summary>
    IReadOnlyList<ReportField> Fields { get; }

    /// <summary>Returns the ordinal of <paramref name="name"/>, or <c>-1</c> if absent.</summary>
    int IndexOf(string name);
}
