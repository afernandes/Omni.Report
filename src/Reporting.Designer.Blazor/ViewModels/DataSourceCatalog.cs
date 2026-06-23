using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Logical type of a designer-time field. Drives the icon/colour in the data tree
/// and the autocomplete shown by the expression editor.</summary>
public enum DesignerFieldType { Text, Number, Date, Bool, Money }

public sealed class DesignerField : Notifying
{
    public DesignerField(string name, DesignerFieldType type, string? sample = null)
    {
        _name = name;
        _type = type;
        _sample = sample;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private DesignerFieldType _type;
    public DesignerFieldType Type { get => _type; set => Set(ref _type, value); }

    private string? _sample;
    public string? Sample { get => _sample; set => Set(ref _sample, value); }

    public string TypeIndicator => Type switch
    {
        DesignerFieldType.Text   => "T",
        DesignerFieldType.Number => "#",
        DesignerFieldType.Date   => "D",
        DesignerFieldType.Bool   => "B",
        DesignerFieldType.Money  => "$",
        _ => "?",
    };

    public string TypeCssClass => Type switch
    {
        DesignerFieldType.Number => "t-num",
        DesignerFieldType.Date   => "t-date",
        DesignerFieldType.Bool   => "t-bool",
        DesignerFieldType.Money  => "t-money",
        _ => "t-text",
    };

    public DesignerField Clone() => new(Name, Type, Sample);
}

/// <summary>One SQL parameter ↔ report-parameter binding row. Edited as a grid in the
/// Query tab of the data source dialog.</summary>
public sealed class DesignerSqlParameter : Notifying
{
    public DesignerSqlParameter(string sqlName, string? reportParameter = null, string? literal = null)
    {
        _sqlName = sqlName;
        _reportParameter = reportParameter;
        _literal = literal;
    }

    private string _sqlName;
    /// <summary>The placeholder as it appears in the SQL (e.g. <c>@dataInicio</c>,
    /// <c>$customer</c>, <c>:id</c>). Keep the provider's prefix.</summary>
    public string SqlName { get => _sqlName; set => Set(ref _sqlName, value); }

    private string? _reportParameter;
    /// <summary>Report parameter name whose runtime value feeds this SQL placeholder. When
    /// non-null, takes precedence over <see cref="Literal"/>.</summary>
    public string? ReportParameter { get => _reportParameter; set => Set(ref _reportParameter, value); }

    private string? _literal;
    /// <summary>Hard-coded literal — used at preview time, and at runtime when
    /// <see cref="ReportParameter"/> is null.</summary>
    public string? Literal { get => _literal; set => Set(ref _literal, value); }

    public DesignerSqlParameter Clone() => new(SqlName, ReportParameter, Literal);
}

/// <summary>RDL <c>&lt;CalculatedField&gt;</c>: a virtual field whose value is computed
/// per row from an expression. Round-trips through <see cref="DataSourceDefinition.CalculatedFields"/>.</summary>
public sealed class DesignerCalculatedField : Notifying
{
    public DesignerCalculatedField(string name, string expression, DesignerFieldType resultType = DesignerFieldType.Text)
    {
        _name = name;
        _expression = expression;
        _resultType = resultType;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression;
    /// <summary>Raw expression (no template braces) — evaluated per row. Can reference
    /// other Fields, Parameters, aggregates, and earlier CalculatedFields.</summary>
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private DesignerFieldType _resultType;
    /// <summary>Logical type hint for the computed value. Drives coercion at evaluation
    /// time when the expression returns a string but the column is supposed to be
    /// numeric / date / etc.</summary>
    public DesignerFieldType ResultType { get => _resultType; set => Set(ref _resultType, value); }
}

public sealed class DesignerDataSource : Notifying
{
    public DesignerDataSource(string name, IEnumerable<DesignerField> fields)
    {
        _name = name;
        Fields = new ObservableCollection<DesignerField>(fields);
        Fields.CollectionChanged += (_, _) => RaiseChanged();
        foreach (var f in Fields) f.Changed += RaiseChanged;
        SqlParameters = new ObservableCollection<DesignerSqlParameter>();
        SqlParameters.CollectionChanged += (_, _) => RaiseChanged();
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    /// <summary>Live, mutable collection of fields. Renames / reorders / adds / removes all
    /// bubble through to the data tree and the expression-editor autocomplete.</summary>
    public ObservableCollection<DesignerField> Fields { get; }

    // ── Live database connection (set when Kind != InMemory) ───────────────────

    private DataConnectionKind _kind = DataConnectionKind.InMemory;
    /// <summary>Provider this data source talks to. Default <see cref="DataConnectionKind.InMemory"/>
    /// (no DB, host supplies data at runtime).</summary>
    public DataConnectionKind Kind { get => _kind; set => Set(ref _kind, value); }

    private string? _connectionString;
    /// <summary>Provider-specific connection string. <b>Persisted</b> in <c>.repx</c> by
    /// default — for production reports prefer secret references or environment overrides
    /// resolved at host start-up.</summary>
    public string? ConnectionString { get => _connectionString; set => Set(ref _connectionString, value); }

    private string? _sql;
    /// <summary>Query text. Should use parameter placeholders (no string concatenation).</summary>
    public string? Sql { get => _sql; set => Set(ref _sql, value); }

    private int? _commandTimeoutSeconds;
    /// <summary>Optional command timeout in seconds. <c>null</c> = provider default (~30s).</summary>
    public int? CommandTimeoutSeconds { get => _commandTimeoutSeconds; set => Set(ref _commandTimeoutSeconds, value); }

    private bool _isStoredProcedure;
    /// <summary>When true, <see cref="Sql"/> is treated as a stored-procedure name.</summary>
    public bool IsStoredProcedure { get => _isStoredProcedure; set => Set(ref _isStoredProcedure, value); }

    /// <summary>SQL placeholder ↔ report-parameter mapping. Empty when the query is static.</summary>
    public ObservableCollection<DesignerSqlParameter> SqlParameters { get; }

    // ── RDL Phase 1: CalculatedFields + Filter + Sort ──────────────────────────

    /// <summary>RDL <c>&lt;CalculatedField&gt;</c> rules — virtual fields whose values are
    /// computed per row from an expression. Each rule exposes <c>Fields.{Name}</c> at runtime.</summary>
    public ObservableCollection<DesignerCalculatedField> CalculatedFields { get; } = new();

    private string? _filterExpression;
    /// <summary>RDL <c>&lt;Filters&gt;</c> on the data source: boolean expression evaluated
    /// per row; non-matching rows are dropped before any region consumes them.</summary>
    public string? FilterExpression { get => _filterExpression; set => Set(ref _filterExpression, value); }

    /// <summary>RDL <c>&lt;SortExpressions&gt;</c> on the data source: ordered list of sort
    /// keys applied to all rows before any region sees them.</summary>
    public ObservableCollection<SortDescriptorRule> SortExpressions { get; } = new();

    public bool IsLiveDatabase => Kind != DataConnectionKind.InMemory;

    public void AddField(DesignerField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.Changed += RaiseChanged;
        Fields.Add(field);
    }

    public void RemoveField(DesignerField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.Changed -= RaiseChanged;
        Fields.Remove(field);
    }

    public void ReplaceFields(IEnumerable<DesignerField> fields)
    {
        foreach (var f in Fields) f.Changed -= RaiseChanged;
        Fields.Clear();
        foreach (var f in fields) AddField(f);
    }

    /// <summary>Maps a list of CLR-typed columns (as returned by
    /// <see cref="IDesignerDataConnect.DiscoverSchemaAsync"/>) into <see cref="DesignerField"/>s
    /// and replaces the current field list. Type inference: integer/decimal/double → Number,
    /// DateTime/DateOnly/TimeSpan → Date, bool → Bool, everything else → Text.</summary>
    public void ReplaceFromDiscovery(IEnumerable<DiscoveredField> discovered)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        var mapped = new List<DesignerField>();
        foreach (var d in discovered)
        {
            mapped.Add(new DesignerField(d.Name, ClassifyClrType(d.ClrType)));
        }
        ReplaceFields(mapped);
    }

    // ClassifyClrType lives below (single source of truth shared by ReplaceFromDiscovery
    // and FromDefinition).

    /// <summary>Parses a JSON sample (object or array of objects) and infers a field list
    /// from the first record's keys. Handles strings, numbers, booleans, ISO/BR dates, and
    /// money-formatted strings. Returns true if at least one field was inferred.</summary>
    public static IReadOnlyList<DesignerField> InferFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<DesignerField>();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return Array.Empty<DesignerField>(); }

        using (doc)
        {
            var root = doc.RootElement;
            // Accept either a top-level array (take first item) or a single object.
            JsonElement record;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0) return Array.Empty<DesignerField>();
                record = root[0];
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                record = root;
            }
            else
            {
                return Array.Empty<DesignerField>();
            }
            if (record.ValueKind != JsonValueKind.Object) return Array.Empty<DesignerField>();

            var fields = new List<DesignerField>();
            foreach (var prop in record.EnumerateObject())
            {
                var (type, sample) = InferFieldType(prop.Value);
                fields.Add(new DesignerField(prop.Name, type, sample));
            }
            return fields;
        }
    }

    private static (DesignerFieldType type, string? sample) InferFieldType(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                return (DesignerFieldType.Bool, value.GetBoolean() ? "true" : "false");
            case JsonValueKind.Number:
                var raw = value.GetRawText();
                return (DesignerFieldType.Number, raw);
            case JsonValueKind.String:
                var s = value.GetString() ?? "";
                if (LooksLikeDate(s))  return (DesignerFieldType.Date,  s);
                if (LooksLikeMoney(s)) return (DesignerFieldType.Money, s);
                return (DesignerFieldType.Text, s);
            case JsonValueKind.Null:
                return (DesignerFieldType.Text, null);
            default:
                // arrays / objects flatten to text representation
                return (DesignerFieldType.Text, value.GetRawText());
        }
    }

    private static bool LooksLikeDate(string s)
    {
        if (s.Length < 6) return false;
        // ISO 8601 yyyy-MM-dd / yyyy-MM-ddTHH:mm or dd/MM/yyyy
        if (s.Length >= 10 && s[4] == '-' && s[7] == '-') return true;
        if (s.Length >= 10 && (s[2] == '/' || s[2] == '-') && (s[5] == '/' || s[5] == '-')) return true;
        return false;
    }

    private static bool LooksLikeMoney(string s)
    {
        // R$ 1.234,56 or $ 1,234.56 or starts with currency symbol
        return s.StartsWith("R$", StringComparison.Ordinal)
            || s.StartsWith("$") || s.StartsWith("€") || s.StartsWith("£");
    }

    // ─── .repx round-trip ───────────────────────────────────────────────────

    /// <summary>Reserved metadata keys persisted inside <see cref="DataSourceDefinition.Parameters"/>.
    /// Underscore prefix distinguishes them from user-supplied SQL parameter names
    /// (which use <c>@</c>/<c>$</c>/<c>:</c> prefixes).</summary>
    internal static class RepxKeys
    {
        public const string Kind     = "_kind";
        public const string Conn     = "_connection";
        public const string Sql      = "_sql";
        public const string StoredProc = "_storedProc";
        public const string Timeout  = "_timeout";
        public const string SqlParamPrefix = "param:";   // ↦ "param:@dataInicio" → "ReportParameter|literalValue"
    }

    /// <summary>Converts the designer view-model into a core <see cref="DataSourceDefinition"/>
    /// ready to serialize to <c>.repx</c>/<c>.repjson</c>. Connection details live in the
    /// flat <c>Parameters</c> dictionary — no Core schema changes needed.</summary>
    public DataSourceDefinition ToDefinition()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Kind != DataConnectionKind.InMemory)
        {
            parameters[RepxKeys.Kind] = Kind.ToString();
        }
        if (!string.IsNullOrEmpty(ConnectionString)) parameters[RepxKeys.Conn] = ConnectionString;
        if (!string.IsNullOrEmpty(Sql))              parameters[RepxKeys.Sql]  = Sql;
        if (IsStoredProcedure)                       parameters[RepxKeys.StoredProc] = "true";
        if (CommandTimeoutSeconds is int t)          parameters[RepxKeys.Timeout] = t.ToString(CultureInfo.InvariantCulture);
        foreach (var sp in SqlParameters)
        {
            if (string.IsNullOrWhiteSpace(sp.SqlName)) continue;
            // Encoded value: "<reportParameter>|<literal>". Either side can be empty.
            var encoded = $"{sp.ReportParameter ?? string.Empty}|{sp.Literal ?? string.Empty}";
            parameters[RepxKeys.SqlParamPrefix + sp.SqlName] = encoded;
        }

        var coreFields = Fields.Select(f => new DataField(f.Name, MapFieldType(f.Type)));
        var calcFields = CalculatedFields.Count == 0
            ? EquatableArray<CalculatedField>.Empty
            : new EquatableArray<CalculatedField>(
                CalculatedFields.Select(c => new CalculatedField(c.Name, c.Expression, MapFieldType(c.ResultType))));
        var sorts = SortExpressions.Count == 0
            ? EquatableArray<Reporting.Data.SortDescriptor>.Empty
            : new EquatableArray<Reporting.Data.SortDescriptor>(
                SortExpressions.Select(s => s.ToDescriptor()));
        return new DataSourceDefinition(
            Name: Name,
            Fields: new EquatableArray<DataField>(coreFields),
            Parameters: new EquatableDictionary<string, string>(parameters),
            CalculatedFields: calcFields,
            FilterExpression: string.IsNullOrWhiteSpace(FilterExpression) ? null : FilterExpression,
            SortExpressions: sorts);
    }

    /// <summary>Round-trip from a <see cref="DataSourceDefinition"/> (just loaded from <c>.repx</c>)
    /// back into a designer view-model. Connection details are recovered from the
    /// <c>Parameters</c> dictionary using the <see cref="RepxKeys"/> conventions.</summary>
    public static DesignerDataSource FromDefinition(DataSourceDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var fields = def.Fields.Select(f => new DesignerField(f.Name, ClassifyClrType(f.FieldType)));
        var ds = new DesignerDataSource(def.Name, fields);

        var pdict = def.Parameters;
        if (pdict.TryGetValue(RepxKeys.Kind, out var kindRaw)
            && Enum.TryParse<DataConnectionKind>(kindRaw, ignoreCase: true, out var kind))
        {
            ds.Kind = kind;
        }
        if (pdict.TryGetValue(RepxKeys.Conn, out var cs)) ds.ConnectionString = cs;
        if (pdict.TryGetValue(RepxKeys.Sql,  out var sql)) ds.Sql = sql;
        if (pdict.TryGetValue(RepxKeys.StoredProc, out var sp)
            && bool.TryParse(sp, out var spBool)) ds.IsStoredProcedure = spBool;
        if (pdict.TryGetValue(RepxKeys.Timeout, out var to)
            && int.TryParse(to, NumberStyles.Integer, CultureInfo.InvariantCulture, out var toInt))
        {
            ds.CommandTimeoutSeconds = toInt;
        }
        foreach (var kv in pdict)
        {
            if (!kv.Key.StartsWith(RepxKeys.SqlParamPrefix, StringComparison.Ordinal)) continue;
            var sqlName = kv.Key.Substring(RepxKeys.SqlParamPrefix.Length);
            var parts = (kv.Value ?? string.Empty).Split('|', 2);
            var reportParam = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : null;
            var literal = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
            ds.SqlParameters.Add(new DesignerSqlParameter(sqlName, reportParam, literal));
        }
        // RDL Phase 1 round-trip: calculated fields + filter + sort.
        foreach (var cf in def.CalculatedFields)
        {
            ds.CalculatedFields.Add(new DesignerCalculatedField(cf.Name, cf.Expression, ClassifyClrType(cf.ResultType)));
        }
        ds.FilterExpression = def.FilterExpression;
        foreach (var s in def.SortExpressions)
        {
            ds.SortExpressions.Add(SortDescriptorRule.From(s));
        }
        return ds;
    }

    private static Type? MapFieldType(DesignerFieldType t) => t switch
    {
        DesignerFieldType.Number => typeof(decimal),
        DesignerFieldType.Date   => typeof(DateTime),
        DesignerFieldType.Bool   => typeof(bool),
        DesignerFieldType.Money  => typeof(decimal),
        _ => typeof(string),
    };

    private static DesignerFieldType ClassifyClrType(Type? clr)
    {
        if (clr is null) return DesignerFieldType.Text;
        clr = Nullable.GetUnderlyingType(clr) ?? clr;
        if (clr == typeof(bool)) return DesignerFieldType.Bool;
        if (clr == typeof(DateTime) || clr == typeof(DateTimeOffset) || clr == typeof(TimeSpan))
            return DesignerFieldType.Date;
        if (clr == typeof(decimal)) return DesignerFieldType.Money;  // best-effort heuristic
        if (clr == typeof(byte) || clr == typeof(sbyte) ||
            clr == typeof(short) || clr == typeof(ushort) ||
            clr == typeof(int) || clr == typeof(uint) ||
            clr == typeof(long) || clr == typeof(ulong) ||
            clr == typeof(float) || clr == typeof(double))
        {
            return DesignerFieldType.Number;
        }
        return DesignerFieldType.Text;
    }

    /// <summary>Sample dataset used by the in-designer preview — keeps the canvas honest
    /// when the host hasn't wired a real data source yet.</summary>
    public static DesignerDataSource SampleVendas { get; } = new("Vendas",
    [
        new("Cliente.Nome",   DesignerFieldType.Text,   "Padaria Real"),
        new("Produto",        DesignerFieldType.Text,   "Farinha 25kg"),
        new("Quantidade",     DesignerFieldType.Number, "12"),
        new("PrecoUnitario",  DesignerFieldType.Money,  "R$ 148,90"),
        new("Total",          DesignerFieldType.Money,  "R$ 1.786,80"),
        new("Data",           DesignerFieldType.Date,   "23/10/2025"),
    ]);
}

/// <summary>One master→detail relationship between two data sources.
/// At paginate time, the detail band iterates child rows filtered by the current parent
/// row's field value (joining on equality).</summary>
public sealed class DesignerRelation : Notifying
{
    public DesignerRelation(string name, string parentSource, string parentField, string childSource, string childField)
    {
        _name = name;
        _parentSource = parentSource;
        _parentField = parentField;
        _childSource = childSource;
        _childField = childField;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _parentSource;
    public string ParentSource { get => _parentSource; set => Set(ref _parentSource, value); }

    private string _parentField;
    public string ParentField { get => _parentField; set => Set(ref _parentField, value); }

    private string _childSource;
    public string ChildSource { get => _childSource; set => Set(ref _childSource, value); }

    private string _childField;
    public string ChildField { get => _childField; set => Set(ref _childField, value); }

    public DesignerRelation Clone() => new(Name, ParentSource, ParentField, ChildSource, ChildField);
}

public sealed class DesignerParameter : Notifying
{
    public DesignerParameter(string name, DesignerFieldType type, string? defaultValue = null)
    {
        _name = name;
        _type = type;
        _defaultValue = defaultValue;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private DesignerFieldType _type;
    public DesignerFieldType Type { get => _type; set => Set(ref _type, value); }

    private string? _defaultValue;
    public string? DefaultValue { get => _defaultValue; set => Set(ref _defaultValue, value); }

    private string? _defaultValueExpression;
    /// <summary>An expression default (SSRS <c>=Today()</c>, <c>=DateAdd(...)</c>) evaluated at run start when no
    /// literal <see cref="DefaultValue"/> and no prompted value are supplied. Mutually exclusive with
    /// <see cref="DefaultValue"/>.</summary>
    public string? DefaultValueExpression { get => _defaultValueExpression; set => Set(ref _defaultValueExpression, value); }

    private string? _prompt;
    /// <summary>RDL <c>&lt;Prompt&gt;</c>: label shown to the user when the report runs.</summary>
    public string? Prompt { get => _prompt; set => Set(ref _prompt, value); }

    private bool _required = true;
    /// <summary>RDL <c>Nullable=false</c>: the report can't run until a value is supplied.</summary>
    public bool Required { get => _required; set => Set(ref _required, value); }

    private bool _allowMultiple;
    /// <summary>RDL <c>&lt;MultiValue&gt;</c>: the parameter accepts a list of values.</summary>
    public bool AllowMultiple { get => _allowMultiple; set => Set(ref _allowMultiple, value); }

    private bool _hidden;
    /// <summary>RDL <c>&lt;Hidden&gt;</c>: the parameter is not shown in the prompt (set via default/query).</summary>
    public bool Hidden { get => _hidden; set => Set(ref _hidden, value); }

    private bool _nullable;
    /// <summary>RDL <c>&lt;Nullable&gt;</c>: the value may be null.</summary>
    public bool Nullable { get => _nullable; set => Set(ref _nullable, value); }

    private bool _allowBlank;
    /// <summary>RDL <c>&lt;AllowBlank&gt;</c>: an empty string is a valid value (string params).</summary>
    public bool AllowBlank { get => _allowBlank; set => Set(ref _allowBlank, value); }

    private string? _availableValuesText;
    /// <summary>Static allowed values (SSRS Available Values), one per line as <c>value</c> or
    /// <c>value|label</c> (split on the first <c>|</c>; values/labels containing <c>|</c> aren't
    /// representable here — use code-first/low-level for those). Empty = no static domain. Combined with
    /// the query fields below.</summary>
    public string? AvailableValuesText { get => _availableValuesText; set => Set(ref _availableValuesText, value); }

    private string? _availableValuesDataSet;
    /// <summary>Query-driven Available Values: dataset to pull the domain from. Empty = static only.</summary>
    public string? AvailableValuesDataSet { get => _availableValuesDataSet; set => Set(ref _availableValuesDataSet, value); }

    private string? _availableValuesValueField;
    /// <summary>Field providing the value, when <see cref="AvailableValuesDataSet"/> is set.</summary>
    public string? AvailableValuesValueField { get => _availableValuesValueField; set => Set(ref _availableValuesValueField, value); }

    private string? _availableValuesLabelField;
    /// <summary>Field providing the display label (falls back to the value), when query-driven.</summary>
    public string? AvailableValuesLabelField { get => _availableValuesLabelField; set => Set(ref _availableValuesLabelField, value); }

    /// <summary>CLR type the runtime coerces prompted input to (maps from <see cref="Type"/>).</summary>
    public System.Type ClrType => Type switch
    {
        DesignerFieldType.Number => typeof(double),
        DesignerFieldType.Money => typeof(decimal),
        DesignerFieldType.Date => typeof(DateTime),
        DesignerFieldType.Bool => typeof(bool),
        _ => typeof(string),
    };

    /// <summary>Materializes the core <see cref="Reporting.Parameters.ReportParameter"/> so the
    /// definition (and .repx) persists the full parameter — not just its name/type.</summary>
    internal Reporting.Parameters.ReportParameter ToReportParameter()
        => new(Name, ClrType,
            Prompt: string.IsNullOrWhiteSpace(Prompt) ? null : Prompt,
            DefaultValue: CoercedDefault(),
            AllowMultiple: AllowMultiple,
            Required: Required,
            AvailableValues: BuildAvailableValues(),
            Nullable: Nullable,
            AllowBlank: AllowBlank,
            Hidden: Hidden)
        {
            DefaultValueExpression = string.IsNullOrWhiteSpace(DefaultValueExpression) ? null : DefaultValueExpression,
        };

    /// <summary>Builds the core <see cref="Reporting.Parameters.ParameterAvailableValues"/> from the
    /// editor's static text + query fields, or <c>null</c> when neither is configured.</summary>
    private Reporting.Parameters.ParameterAvailableValues? BuildAvailableValues()
    {
        var values = ParseValueLines(AvailableValuesText);
        bool hasQuery = !string.IsNullOrWhiteSpace(AvailableValuesDataSet)
                        && !string.IsNullOrWhiteSpace(AvailableValuesValueField);
        if (values.Length == 0 && !hasQuery)
        {
            return null;
        }
        return new Reporting.Parameters.ParameterAvailableValues
        {
            Values = new Reporting.Common.EquatableArray<Reporting.Parameters.ParameterValue>(values),
            DataSet = hasQuery ? AvailableValuesDataSet : null,
            ValueField = hasQuery ? AvailableValuesValueField : null,
            LabelField = hasQuery && !string.IsNullOrWhiteSpace(AvailableValuesLabelField) ? AvailableValuesLabelField : null,
        };
    }

    // Parses one "value" or "value|label" per line into static parameter values. Blank lines and lines
    // whose value part is empty (e.g. "|orphan") are skipped; an empty label ("X|") collapses to null so
    // it canonicalizes back to plain "X" on reload. The value/label split is on the FIRST '|'.
    private static Reporting.Parameters.ParameterValue[] ParseValueLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Reporting.Parameters.ParameterValue>();
        }
        var result = new List<Reporting.Parameters.ParameterValue>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            int bar = line.IndexOf('|');
            var value = (bar >= 0 ? line[..bar] : line).Trim();
            if (value.Length == 0)
            {
                continue;
            }
            var label = bar >= 0 ? line[(bar + 1)..].Trim() : string.Empty;
            result.Add(new Reporting.Parameters.ParameterValue(value, label.Length > 0 ? label : null));
        }
        return result.ToArray();
    }

    /// <summary>Coerces the editor's string <see cref="DefaultValue"/> into the parameter's CLR type
    /// so the serializer (which round-trips <c>object</c> defaults) gets a typed value. An empty or
    /// unparseable typed default becomes <c>null</c> — never a type-mismatched string that would
    /// throw when serialized.</summary>
    private object? CoercedDefault()
    {
        if (string.IsNullOrWhiteSpace(DefaultValue)) return null;
        if (Type == DesignerFieldType.Text) return DefaultValue;
        foreach (var ci in new[] { System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.InvariantCulture })
        {
            try
            {
                return Type switch
                {
                    DesignerFieldType.Number => Convert.ToDouble(DefaultValue, ci),
                    DesignerFieldType.Money => Convert.ToDecimal(DefaultValue, ci),
                    DesignerFieldType.Date => DateTime.Parse(DefaultValue, ci),
                    DesignerFieldType.Bool => bool.Parse(DefaultValue),
                    _ => (object)DefaultValue,
                };
            }
            catch (FormatException) { }
            catch (OverflowException) { }
        }
        return null;
    }

    /// <summary>Rebuilds a designer parameter from a loaded core parameter (reverse of
    /// <see cref="ToReportParameter"/>) so a .repx round-trips every field.</summary>
    internal static DesignerParameter From(Reporting.Parameters.ReportParameter p)
        => new(p.Name, ClassifyClr(p.ValueType), p.DefaultValue?.ToString())
        {
            DefaultValueExpression = p.DefaultValueExpression,
            Prompt = p.Prompt,
            Required = p.Required,
            AllowMultiple = p.AllowMultiple,
            Hidden = p.Hidden,
            Nullable = p.Nullable,
            AllowBlank = p.AllowBlank,
            AvailableValuesText = p.AvailableValues is { Values.Count: > 0 } av
                ? string.Join("\n", av.Values.Select(v => v.Label is null ? v.Value : $"{v.Value}|{v.Label}"))
                : null,
            AvailableValuesDataSet = p.AvailableValues?.DataSet,
            AvailableValuesValueField = p.AvailableValues?.ValueField,
            AvailableValuesLabelField = p.AvailableValues?.LabelField,
        };

    private static DesignerFieldType ClassifyClr(System.Type t)
    {
        if (t == typeof(bool)) return DesignerFieldType.Bool;
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return DesignerFieldType.Date;
        if (t == typeof(decimal)) return DesignerFieldType.Money;
        if (t == typeof(double) || t == typeof(float) || t == typeof(int) || t == typeof(long)) return DesignerFieldType.Number;
        return DesignerFieldType.Text;
    }
}

/// <summary>A report-level computed variable in the designer — name + expression + scope, persisted as a
/// core <see cref="Reporting.Parameters.ReportVariable"/> (RDL <c>&lt;Variables&gt;</c>).</summary>
public sealed class DesignerVariable : Notifying
{
    public DesignerVariable(string name, string expression = "", Reporting.Parameters.VariableScope scope = Reporting.Parameters.VariableScope.Report)
    {
        _name = name;
        _expression = expression;
        _scope = scope;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression;
    /// <summary>Expression evaluated to produce the variable's value (e.g. <c>Sum(Fields.Total)</c>).</summary>
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private Reporting.Parameters.VariableScope _scope;
    /// <summary>When the variable is evaluated: per row, once per report, or once per group.</summary>
    public Reporting.Parameters.VariableScope Scope { get => _scope; set => Set(ref _scope, value); }

    internal Reporting.Parameters.ReportVariable ToVariable()
        => new(Name, Expression, Scope);

    internal static DesignerVariable From(Reporting.Parameters.ReportVariable v)
        => new(v.Name, v.Expression, v.Scope);
}
