namespace Reporting.Serialization;

/// <summary>
/// Migration applied to a freshly-loaded XML document when its <c>SchemaVersion</c> is
/// older than <see cref="SchemaVersion.Current"/>. Migrations transform the
/// <see cref="System.Xml.Linq.XDocument"/> in-place and run in <see cref="From"/> order.
/// </summary>
public interface IRepxMigration
{
    /// <summary>Schema version BEFORE this migration applies (the version being upgraded FROM).</summary>
    SchemaVersion From { get; }

    /// <summary>Schema version AFTER this migration ran (the version being upgraded TO).</summary>
    SchemaVersion To { get; }

    void Apply(System.Xml.Linq.XDocument document);
}
