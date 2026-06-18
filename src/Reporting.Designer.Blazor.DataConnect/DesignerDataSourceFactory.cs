using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Reporting.DataSources;
using Reporting.DataSources.AdoNet;
using Reporting.Designer.Blazor.ViewModels;

namespace Reporting.Designer.Blazor.DataConnect;

/// <summary>
/// Builds a real <see cref="IReportDataSource"/> at paginate-time from a designer
/// <see cref="DesignerDataSource"/> view-model. Resolves SQL parameters by looking up the
/// current report parameter values from a runtime dictionary.
/// </summary>
/// <remarks>
/// <para>The flow at render time:</para>
/// <list type="number">
/// <item>Host calls <c>report.PaginateAsync(parameters: {...})</c>.</item>
/// <item>Before that, the host (or designer host) calls
/// <see cref="Build"/> with each <see cref="DesignerDataSource"/> and the same parameter
/// dictionary; the result is registered on <c>report.DataSources</c>.</item>
/// <item>The paginator pulls rows via <see cref="IReportDataSource.ReadAsync"/> normally.</item>
/// </list>
///
/// <para>For <see cref="DataConnectionKind.InMemory"/> sources, this factory returns
/// <c>null</c> — the host must register an <c>EnumerableDataSource&lt;T&gt;</c> or
/// equivalent themselves.</para>
/// </remarks>
public static class DesignerDataSourceFactory
{
    /// <summary>Materializes a runtime data source from a designer view-model. Returns
    /// <c>null</c> for in-memory or invalid configurations — caller handles those.</summary>
    /// <param name="vm">The designer-side view-model describing the connection.</param>
    /// <param name="reportParameters">Runtime values for the report parameters; used to
    /// resolve SQL placeholders bound via <see cref="DesignerSqlParameter.ReportParameter"/>.
    /// Pass <c>null</c> or empty when previewing with literals only.</param>
    /// <param name="secretResolver">Optional resolver for <c>{secret:NAME}</c> placeholders
    /// embedded in the connection string. When <c>null</c>, placeholders are expanded
    /// from environment variables (<see cref="EnvironmentSecretResolver"/>).</param>
    public static IReportDataSource? Build(
        DesignerDataSource vm,
        IReadOnlyDictionary<string, object?>? reportParameters = null,
        ISecretResolver? secretResolver = null)
    {
        ArgumentNullException.ThrowIfNull(vm);
        if (vm.Kind == DataConnectionKind.InMemory) return null;
        if (string.IsNullOrWhiteSpace(vm.ConnectionString)) return null;
        if (string.IsNullOrWhiteSpace(vm.Sql)) return null;

        var resolved = ResolveSqlParameters(vm, reportParameters);
        var cs = ExpandSecrets(vm.ConnectionString!, secretResolver);

        // Each kind has a thin wrapper that owns the connection factory + AdoNetDataSource.
        return vm.Kind switch
        {
            DataConnectionKind.Sqlite => BuildAdoNet(vm, () => new SqliteConnection(cs), resolved),
            DataConnectionKind.PostgreSql => BuildAdoNet(vm, () => new NpgsqlConnection(cs), resolved),
            DataConnectionKind.SqlServer => BuildAdoNet(vm, () => new SqlConnection(cs), resolved),
            DataConnectionKind.MySql => BuildAdoNet(vm, () => new MySqlConnection(cs), resolved),
            _ => null,
        };
    }

    /// <summary>Builds a runtime data source that <em>reuses</em> a caller-managed
    /// <see cref="System.Data.Common.DbConnection"/> (typically inside a unit-of-work /
    /// open transaction). The connection is never opened or closed by the source.</summary>
    /// <remarks>The connection's provider must match <paramref name="vm"/>.<c>Kind</c>
    /// — caller is responsible. Use this overload when reports must read from the same
    /// transaction as surrounding code (consistent snapshot, share locks, etc.).</remarks>
    public static IReportDataSource? BuildWithConnection(
        DesignerDataSource vm,
        System.Data.Common.DbConnection openConnection,
        IReadOnlyDictionary<string, object?>? reportParameters = null)
    {
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentNullException.ThrowIfNull(openConnection);
        if (vm.Kind == DataConnectionKind.InMemory) return null;
        if (string.IsNullOrWhiteSpace(vm.Sql)) return null;
        var resolved = ResolveSqlParameters(vm, reportParameters);
        var query = new AdoNetQuery(
            Sql: vm.Sql!,
            Parameters: resolved,
            CommandType: vm.IsStoredProcedure ? System.Data.CommandType.StoredProcedure : System.Data.CommandType.Text,
            CommandTimeoutSeconds: vm.CommandTimeoutSeconds);
        return new AdoNetDataSource(vm.Name, openConnection, query);
    }

    /// <summary>Materializes a runtime <see cref="DataSourceRegistry"/> from every view-model
    /// in <paramref name="vms"/>, then wires master-detail relations as
    /// <see cref="MasterDetailDataSource"/> views. The returned registry is ready to drop
    /// into <c>report.DataSources</c>.</summary>
    public static DataSourceRegistry BuildRegistry(
        IEnumerable<DesignerDataSource> vms,
        IEnumerable<DesignerRelation> relations,
        IReadOnlyDictionary<string, object?>? reportParameters = null,
        ISecretResolver? secretResolver = null)
    {
        ArgumentNullException.ThrowIfNull(vms);
        ArgumentNullException.ThrowIfNull(relations);
        var registry = new DataSourceRegistry();
        var built = new Dictionary<string, IReportDataSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in vms)
        {
            var src = Build(vm, reportParameters, secretResolver);
            if (src is null) continue;
            registry.Register(src);
            built[vm.Name] = src;
        }
        // Layer master-detail views on top of the registered children. The detail report
        // band looks up the relation's name in the registry and binds parent keys at iteration.
        foreach (var rel in relations)
        {
            if (string.IsNullOrWhiteSpace(rel.Name) ||
                string.IsNullOrWhiteSpace(rel.ChildSource) ||
                string.IsNullOrWhiteSpace(rel.ChildField))
            {
                continue;
            }
            if (!built.TryGetValue(rel.ChildSource, out var child)) continue;
            var view = new MasterDetailDataSource(rel.Name, child, rel.ChildField);
            registry.Register(view);
        }
        return registry;
    }

    /// <summary>Resolves <c>{secret:NAME}</c> placeholders in the connection string. Uses the
    /// supplied resolver when available, otherwise falls back to environment variables
    /// — synchronously, because the runtime path is not async-only.</summary>
    private static string ExpandSecrets(string connectionString, ISecretResolver? resolver)
    {
        if (!SecretTemplate.ContainsPlaceholder(connectionString)) return connectionString;
        if (resolver is null)
        {
            return SecretTemplate.ExpandFromEnvironment(connectionString);
        }
        // Synchronously await the resolver — the factory runs during paginate setup, which
        // is itself async, so blocking here is acceptable. Callers that need a fully async
        // path can pre-expand before calling Build.
        return SecretTemplate.ExpandAsync(connectionString, resolver).GetAwaiter().GetResult();
    }

    private static AdoNetDataSource BuildAdoNet(
        DesignerDataSource vm,
        Func<System.Data.Common.DbConnection> connectionFactory,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var query = new AdoNetQuery(
            Sql: vm.Sql!,
            Parameters: parameters,
            CommandType: vm.IsStoredProcedure ? System.Data.CommandType.StoredProcedure : System.Data.CommandType.Text,
            CommandTimeoutSeconds: vm.CommandTimeoutSeconds);

        return new AdoNetDataSource(vm.Name, connectionFactory, query);
    }

    /// <summary>Builds the final SQL parameter dictionary by resolving each
    /// <see cref="DesignerSqlParameter"/> against the runtime report parameters.
    /// Resolution priority: bound report parameter (when set) → literal fallback.</summary>
    private static IReadOnlyDictionary<string, object?> ResolveSqlParameters(
        DesignerDataSource vm,
        IReadOnlyDictionary<string, object?>? reportParameters)
    {
        if (vm.SqlParameters.Count == 0)
        {
            return new Dictionary<string, object?>(0);
        }
        var dict = new Dictionary<string, object?>(vm.SqlParameters.Count, StringComparer.Ordinal);
        foreach (var sp in vm.SqlParameters)
        {
            if (string.IsNullOrWhiteSpace(sp.SqlName)) continue;
            object? value = null;
            if (!string.IsNullOrEmpty(sp.ReportParameter)
                && reportParameters is not null
                && reportParameters.TryGetValue(sp.ReportParameter, out var bound))
            {
                value = bound;
            }
            else
            {
                value = sp.Literal;
            }
            dict[sp.SqlName] = value;
        }
        return dict;
    }
}
