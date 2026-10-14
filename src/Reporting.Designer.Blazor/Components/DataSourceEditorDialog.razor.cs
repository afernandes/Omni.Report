using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
namespace Reporting.Designer.Blazor.Components;

public partial class DataSourceEditorDialog
{

    [Parameter] public bool Visible { get; set; }
    [Parameter] public DesignerDataSource? Source { get; set; }
    [Parameter] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = Array.Empty<DesignerParameter>();
    /// <summary>All data sources in the catalog — used by the Relations tab to populate
    /// the parent-source dropdown. Excludes the source being edited.</summary>
    [Parameter] public IReadOnlyList<DesignerDataSource> AllDataSources { get; set; } = Array.Empty<DesignerDataSource>();
    /// <summary>Live collection of master-detail relations. The dialog mutates this directly
    /// (no apply/cancel snapshot) because relations are catalog-scoped, not per-source —
    /// canceling the dialog shouldn't roll back a relation deletion any more than canceling
    /// it should roll back another source's edits.</summary>
    [Parameter] public System.Collections.ObjectModel.ObservableCollection<DesignerRelation>? Relations { get; set; }
    [Parameter] public string Theme { get; set; } = "vs";
    [Parameter] public EventCallback<DesignerDataSource> OnApplied { get; set; }
    [Parameter] public EventCallback<DesignerDataSource> OnDelete { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    // Working buffers — the dialog operates on a copy and only commits on Apply.
    private string _name = string.Empty;
    private DataConnectionKind _kind = DataConnectionKind.InMemory;
    private string? _connectionString;
    private string? _sql;
    private bool _isStoredProcedure;
    private int? _commandTimeoutSeconds;
    private readonly List<DesignerField> _fields = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<DesignerSqlParameter> _sqlParameters = new();

    private bool _showJsonImport;
    private string _jsonSample = string.Empty;
    private string? _jsonError;
    private DesignerDataSource? _trackedSource;

    private DsTab _tab = DsTab.Connection;
    private bool _busy;
    private TestConnectionResult? _testResult;
    private DataPreviewResult? _previewResult;
    private string? _discoveryMessage;
    private bool _discoveryError;

    private MonacoEditor? _sqlEditor;
    private readonly string _sqlEditorId = "sql-monaco-" + Guid.NewGuid().ToString("n")[..8];

    // Schema explorer + procedure discovery state
    private IReadOnlyList<DatabaseTable> _schemaTables = Array.Empty<DatabaseTable>();
    private string? _schemaError;
    private string? _schemaInsertHint;

    /// <summary>Optional service — when null the dialog still works but Test/Discover/Preview
    /// buttons are disabled with an inline hint.</summary>
    private IDesignerDataConnect? _dataConnect;

    protected override void OnInitialized()
    {
        _dataConnect = Services.GetService(typeof(IDesignerDataConnect)) as IDesignerDataConnect;
    }

    protected override void OnParametersSet()
    {
        if (Visible && !ReferenceEquals(_trackedSource, Source))
        {
            _trackedSource = Source;
            if (Source is not null)
            {
                _name = Source.Name;
                _kind = Source.Kind;
                _connectionString = Source.ConnectionString;
                _sql = Source.Sql;
                _isStoredProcedure = Source.IsStoredProcedure;
                _commandTimeoutSeconds = Source.CommandTimeoutSeconds;
                _fields.Clear();
                foreach (var f in Source.Fields)
                {
                    _fields.Add(f.Clone());
                }
                _sqlParameters.Clear();
                foreach (var p in Source.SqlParameters)
                {
                    _sqlParameters.Add(p.Clone());
                }
            }
            else
            {
                _name = string.Empty;
                _kind = DataConnectionKind.InMemory;
                _connectionString = null;
                _sql = null;
                _isStoredProcedure = false;
                _commandTimeoutSeconds = null;
                _fields.Clear();
                _sqlParameters.Clear();
            }
            _tab = DsTab.Connection;
            _busy = false;
            _testResult = null;
            _previewResult = null;
            _discoveryMessage = null;
            _showJsonImport = false;
            _jsonSample = string.Empty;
            _jsonError = null;
        }
        else if (!Visible)
        {
            _trackedSource = null;
        }
    }

    private string TabCss(DsTab tab) => tab == _tab ? "ds-tab ds-tab-active" : "ds-tab";

    private string PlaceholderFor(DataConnectionKind kind) => kind switch
    {
        DataConnectionKind.Sqlite => "Data Source=app.db",
        DataConnectionKind.PostgreSql => "Host=localhost;Username=postgres;Password=secret;Database=erp",
        DataConnectionKind.SqlServer => "Server=.\\SQLEXPRESS;Database=erp;Integrated Security=true;TrustServerCertificate=true",
        DataConnectionKind.MySql => "Server=localhost;Database=erp;User=app;Password=secret",
        _ => string.Empty,
    };

    private string SqlParamPlaceholder() => _kind switch
    {
        DataConnectionKind.Sqlite => "$dataInicio",
        DataConnectionKind.PostgreSql => "@dataInicio",
        DataConnectionKind.SqlServer => "@dataInicio",
        DataConnectionKind.MySql => "@dataInicio",
        _ => "@nome",
    };

    private void OnKindChanged(DataConnectionKind kind)
    {
        _kind = kind;
        _testResult = null;
        _previewResult = null;
        _discoveryMessage = null;
        // Auto-navigate to Connection tab when switching to a live provider; back to Fields
        // when switching to in-memory.
        _tab = kind == DataConnectionKind.InMemory ? DsTab.Fields : DsTab.Connection;
    }

    private void AddBlankField()
    {
        var n = $"Campo{_fields.Count + 1}";
        _fields.Add(new DesignerField(n, DesignerFieldType.Text));
    }

    private void AddSqlParameter()
    {
        _sqlParameters.Add(new DesignerSqlParameter(SqlParamPlaceholder()));
    }

    private void ImportJson()
    {
        _jsonError = null;
        var inferred = DesignerDataSource.InferFromJson(_jsonSample);
        if (inferred.Count == 0)
        {
            _jsonError = "JSON inválido ou sem campos no primeiro registro.";
            return;
        }
        _fields.Clear();
        foreach (var f in inferred)
        {
            _fields.Add(f);
        }
        _showJsonImport = false;
        _jsonSample = string.Empty;
    }

    /// <summary>Pulls the latest SQL from Monaco — but only when the Query tab is currently
    /// active. On other tabs the editor is unmounted by Blazor and reading from a stale
    /// <c>@ref</c> returns an empty string, which would wipe <c>_sql</c>. <c>OnValueChanged</c>
    /// keeps <c>_sql</c> in sync continuously while Query is open, so the cached value is
    /// fresh once we leave the tab.</summary>
    private async Task SyncSqlFromEditorIfActiveAsync()
    {
        if (_tab == DsTab.Query && _sqlEditor is not null)
        {
            _sql = await _sqlEditor.GetValueAsync();
        }
    }

    private async Task OnTestConnection()
    {
        if (_dataConnect is null)
        {
            return;
        }
        _busy = true;
        _testResult = null;
        try
        {
            await SyncSqlFromEditorIfActiveAsync();
            _testResult = await _dataConnect.TestConnectionAsync(_kind, _connectionString ?? string.Empty);
        }
        finally { _busy = false; }
    }

    private async Task OnDiscoverFields()
    {
        if (_dataConnect is null)
        {
            return;
        }
        _busy = true;
        _discoveryMessage = null;
        _discoveryError = false;
        try
        {
            await SyncSqlFromEditorIfActiveAsync();
            var paramsDict = BuildPreviewParameters();
            var result = await _dataConnect.DiscoverSchemaAsync(
                _kind, _connectionString ?? string.Empty, _sql ?? string.Empty, paramsDict);

            if (!string.IsNullOrEmpty(result.Error))
            {
                _discoveryMessage = result.Error;
                _discoveryError = true;
            }
            else
            {
                ReplaceFieldsFromDiscovery(result.Fields);
                _discoveryMessage = $"✓ {result.Fields.Count} campo(s) sincronizado(s) em {result.Elapsed.TotalMilliseconds:0} ms.";
                _tab = DsTab.Fields;
            }
        }
        finally { _busy = false; }
    }

    private void OnSchemaTabOpened()
    {
        _tab = DsTab.Schema;
        // Auto-load the schema the first time the tab is opened so the user doesn't have
        // to click Refresh. Subsequent navigations preserve the cached list.
        if (_schemaTables.Count == 0 && _dataConnect is not null)
        {
            _ = OnLoadSchema();
        }
    }

    private async Task OnLoadSchema()
    {
        if (_dataConnect is null)
        {
            return;
        }
        _busy = true;
        _schemaError = null;
        try
        {
            var result = await _dataConnect.ListSchemaAsync(_kind, _connectionString ?? string.Empty);
            if (!string.IsNullOrEmpty(result.Error))
            {
                _schemaError = result.Error;
                _schemaTables = Array.Empty<DatabaseTable>();
            }
            else
            {
                _schemaTables = result.Tables;
                _schemaError = null;
            }
        }
        finally { _busy = false; }
    }

    private async Task OnInsertTableIntoSql(DatabaseTable table)
    {
        var columns = table.Columns.Count > 0
            ? string.Join(", ", table.Columns.Take(20).Select(c => c.Name))
            : "*";
        var snippet = $"SELECT {columns} FROM {table.QualifiedName}";
        // Always update the cached buffer first — Monaco's InitialValue picks this up when
        // the Query tab mounts. If Monaco is already mounted, also push live via SetValueAsync.
        _sql = snippet;
        if (_tab == DsTab.Query && _sqlEditor is not null)
        {
            await _sqlEditor.SetValueAsync(snippet);
        }
        _schemaInsertHint = $"✓ Query gerada para {table.QualifiedName}";
        _tab = DsTab.Query;
    }

    private async Task OnInsertColumnIntoSql((DatabaseTable Table, DatabaseColumn Column) item)
    {
        // Same guarding as OnInsertTableIntoSql — only talk to Monaco when it's mounted.
        if (_tab == DsTab.Query && _sqlEditor is not null)
        {
            await _sqlEditor.InsertAsync(item.Column.Name);
            _sql = await _sqlEditor.GetValueAsync();
        }
        else
        {
            // Append to the cached buffer so it surfaces when the user goes back to Query.
            _sql = (_sql ?? string.Empty) + item.Column.Name;
        }
        _schemaInsertHint = $"✓ Coluna {item.Column.Name} inserida";
    }

    private void OnStoredProcedureToggled(bool value)
    {
        _isStoredProcedure = value;
        // Clear preview/discovery error trail — the SQL meaning changed.
        _discoveryMessage = null;
        _previewResult = null;
    }

    private async Task OnDiscoverProcSignature()
    {
        if (_dataConnect is null || string.IsNullOrWhiteSpace(_sql))
        {
            return;
        }
        _busy = true;
        _discoveryMessage = null;
        _discoveryError = false;
        try
        {
            await SyncSqlFromEditorIfActiveAsync();
            var result = await _dataConnect.DiscoverProcedureSignatureAsync(
                _kind, _connectionString ?? string.Empty, _sql!.Trim());

            if (!string.IsNullOrEmpty(result.Error))
            {
                _discoveryMessage = result.Error;
                _discoveryError = true;
            }
            else
            {
                // Merge: keep existing bindings when the SQL name matches, add new ones.
                var existing = _sqlParameters.ToDictionary(p => p.SqlName, StringComparer.OrdinalIgnoreCase);
                _sqlParameters.Clear();
                foreach (var p in result.Parameters)
                {
                    if (p.IsOutput)
                    {
                        continue; // output params aren't supplied by the caller
                    }
                    if (existing.TryGetValue(p.Name, out var prior))
                    {
                        _sqlParameters.Add(prior);
                    }
                    else
                    {
                        _sqlParameters.Add(new DesignerSqlParameter(p.Name));
                    }
                }
                _discoveryMessage = $"✓ {_sqlParameters.Count} parâmetro(s) detectado(s) em {result.Elapsed.TotalMilliseconds:0} ms.";
            }
        }
        finally { _busy = false; }
    }

    private async Task OnPreview()
    {
        if (_dataConnect is null)
        {
            return;
        }
        _busy = true;
        _previewResult = null;
        try
        {
            await SyncSqlFromEditorIfActiveAsync();
            var paramsDict = BuildPreviewParameters();
            _previewResult = await _dataConnect.PreviewAsync(
                _kind, _connectionString ?? string.Empty, _sql ?? string.Empty, paramsDict, maxRows: 50);
        }
        finally { _busy = false; }
    }

    /// <summary>Builds a parameter dictionary for design-time preview: each entry uses the
    /// literal (when set) or the report-parameter default value (when bound). Runtime
    /// resolution at paginate time follows a similar path but with the actual parameter
    /// values supplied by the host.</summary>
    private IReadOnlyDictionary<string, object?> BuildPreviewParameters()
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in _sqlParameters)
        {
            if (string.IsNullOrWhiteSpace(p.SqlName))
            {
                continue;
            }
            object? value = null;
            if (!string.IsNullOrEmpty(p.ReportParameter))
            {
                var rp = Parameters.FirstOrDefault(x => x.Name == p.ReportParameter);
                value = rp?.DefaultValue ?? p.Literal;
            }
            else
            {
                value = p.Literal;
            }
            dict[p.SqlName] = value;
        }
        return dict;
    }

    private void ReplaceFieldsFromDiscovery(IReadOnlyList<DiscoveredField> discovered)
    {
        _fields.Clear();
        foreach (var d in discovered)
        {
            _fields.Add(new DesignerField(d.Name, ClassifyClrType(d.ClrType)));
        }
    }

    private static DesignerFieldType ClassifyClrType(Type clr)
    {
        clr = Nullable.GetUnderlyingType(clr) ?? clr;
        if (clr == typeof(bool))
        {
            return DesignerFieldType.Bool;
        }
        if (clr == typeof(DateTime) || clr == typeof(DateTimeOffset) || clr == typeof(TimeSpan))
        {
            return DesignerFieldType.Date;
        }
        if (clr == typeof(byte) || clr == typeof(sbyte) ||
                    clr == typeof(short) || clr == typeof(ushort) ||
                    clr == typeof(int) || clr == typeof(uint) ||
                    clr == typeof(long) || clr == typeof(ulong) ||
                    clr == typeof(float) || clr == typeof(double) ||
                    clr == typeof(decimal))
        {
            return DesignerFieldType.Number;
        }
        return DesignerFieldType.Text;
    }

    private static string FormatCell(object? value)
    {
        if (value is null)
        {
            return "—";
        }
        if (value is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
        if (value is double d)
        {
            return d.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (value is decimal m)
        {
            return m.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }
        var s = value.ToString() ?? string.Empty;
        return s.Length > 80 ? s[..80] + "…" : s;
    }

    private async Task HandleApply()
    {
        if (Source is null)
        {
            return;
        }
        if (_sqlEditor is not null)
        {
            _sql = await _sqlEditor.GetValueAsync();
        }
        Source.Name = _name.Trim();
        Source.Kind = _kind;
        Source.ConnectionString = string.IsNullOrWhiteSpace(_connectionString) ? null : _connectionString;
        Source.Sql = string.IsNullOrWhiteSpace(_sql) ? null : _sql;
        Source.IsStoredProcedure = _isStoredProcedure;
        Source.CommandTimeoutSeconds = _commandTimeoutSeconds;
        Source.ReplaceFields(_fields);
        // Replace SQL parameters wholesale (clone again to keep dialog buffer independent).
        Source.SqlParameters.Clear();
        foreach (var p in _sqlParameters)
        {
            Source.SqlParameters.Add(p.Clone());
        }
        await OnApplied.InvokeAsync(Source);
        await OnClosed.InvokeAsync();
    }

    private async Task HandleDelete()
    {
        if (Source is null)
        {
            return;
        }
        await OnDelete.InvokeAsync(Source);
        await OnClosed.InvokeAsync();
    }

    // ── Relations tab helpers ────────────────────────────────────────────────
    /// <summary>Other data sources that could legitimately be parents of this one
    /// (i.e. every source except the one being edited). Used to populate the parent
    /// dropdown and to gate "+ Adicionar pai" availability.</summary>
    private IReadOnlyList<DesignerDataSource> AvailableParents()
        => AllDataSources.Where(d => !string.Equals(d.Name, _name, StringComparison.Ordinal)).ToList();

    /// <summary>How many relations currently have this source on the child side.
    /// Drives the tab's badge count and the empty-state message.</summary>
    private int MyAsChildCount()
        => Relations?.Count(r => string.Equals(r.ChildSource, _name, StringComparison.Ordinal)) ?? 0;

    /// <summary>Field names of <paramref name="sourceName"/> as currently in the catalog.
    /// Returns empty when the source has been deleted (stale relation) — the dropdown
    /// will then surface only the chosen-but-missing value so the user can repair it.</summary>
    private IEnumerable<string> FieldsOfSource(string sourceName)
        => AllDataSources.FirstOrDefault(d => string.Equals(d.Name, sourceName, StringComparison.Ordinal))
                         ?.Fields.Select(f => f.Name)
           ?? Enumerable.Empty<string>();

    private void AddRelationAsChild()
    {
        if (Relations is null)
        {
            return;
        }
        var parent = AvailableParents().FirstOrDefault();
        if (parent is null)
        {
            return;
        }
        var pf = parent.Fields.FirstOrDefault()?.Name ?? string.Empty;
        var cf = _fields.FirstOrDefault()?.Name ?? string.Empty;
        var n = $"Rel_{parent.Name}_{_name}";
        Relations.Add(new DesignerRelation(n, parent.Name, pf, _name, cf));
    }

    // ── RDL Calculated / Filter / Sort helpers ──────────────────────────────────

    /// <summary>Appends a blank calculated field to the source. The user fills in name
    /// + expression + result type inline in the tab body.</summary>
    private void AddCalculatedField()
    {
        if (Source is null)
        {
            return;
        }
        var n = $"Calculada{Source.CalculatedFields.Count + 1}";
        Source.CalculatedFields.Add(new DesignerCalculatedField(n, "Fields.", DesignerFieldType.Text));
    }

    /// <summary>Appends a new sort key (ascending by default) to the source's global sort.</summary>
    private void AddSortKey()
    {
        Source?.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields." });
    }

    /// <summary>Coerces empty / whitespace strings to null — the serializer treats them as
    /// equivalent and storing whitespace would break round-trip equality.</summary>
    private static string? AsNullableTrim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>When the parent source changes, auto-reset the parent field to the first
    /// available column — otherwise the old field name would point at a column that
    /// doesn't exist in the new parent.</summary>
    private void OnParentSourceChanged(DesignerRelation r, string? newParent)
    {
        if (string.IsNullOrEmpty(newParent))
        {
            return;
        }
        r.ParentSource = newParent;
        var first = FieldsOfSource(newParent).FirstOrDefault();
        if (first is not null)
        {
            r.ParentField = first;
        }
    }
}
