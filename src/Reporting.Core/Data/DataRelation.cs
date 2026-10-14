using Reporting.Common;

namespace Reporting.Data;

/// <summary>Master-detail relationship between two data sources.</summary>
public sealed record DataRelation(
    string Name,
    string ParentSource,
    string ParentField,
    string ChildSource,
    string ChildField);
