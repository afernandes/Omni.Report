using System.Data;
using System.Data.Common;

namespace Reporting.DataSources.AdoNet;

/// <summary>
/// SQL statement plus optional parameters and execution knobs. Immutable; safe to share
/// across multiple <see cref="AdoNetDataSource"/> instantiations.
/// </summary>
/// <param name="Sql">The query text. Always use parameter placeholders for user-supplied
/// values — never string-concatenate (SQL injection).</param>
/// <param name="Parameters">Optional name → value pairs bound as <see cref="DbParameter"/>s.
/// The parameter prefix (<c>@</c>, <c>:</c>, <c>$</c>) is provider-specific — include it in
/// the dictionary key as the provider expects.</param>
/// <param name="CommandType">Defaults to <see cref="CommandType.Text"/>. Set to
/// <see cref="CommandType.StoredProcedure"/> when invoking a sproc.</param>
/// <param name="CommandTimeoutSeconds">Optional command timeout. Defaults to the provider's
/// own default (typically 30s) when null.</param>
public sealed record AdoNetQuery(
    string Sql,
    IReadOnlyDictionary<string, object?>? Parameters = null,
    CommandType CommandType = CommandType.Text,
    int? CommandTimeoutSeconds = null);
