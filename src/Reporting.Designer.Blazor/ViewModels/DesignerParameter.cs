using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

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

    private string? _availableValuesFilterField;
    /// <summary>Cascading: the query dataset field matched against the parent parameter's value
    /// (e.g. "Estado" to restrict cities). Requires <see cref="AvailableValuesDependsOn"/>.</summary>
    public string? AvailableValuesFilterField { get => _availableValuesFilterField; set => Set(ref _availableValuesFilterField, value); }

    private string? _availableValuesDependsOn;
    /// <summary>Cascading: the parent parameter whose value restricts this query-driven domain.</summary>
    public string? AvailableValuesDependsOn { get => _availableValuesDependsOn; set => Set(ref _availableValuesDependsOn, value); }

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
            FilterField = hasQuery && !string.IsNullOrWhiteSpace(AvailableValuesFilterField) ? AvailableValuesFilterField : null,
            DependsOn = hasQuery && !string.IsNullOrWhiteSpace(AvailableValuesDependsOn) ? AvailableValuesDependsOn : null,
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
        try { return ParameterValueConverter.Parse(DefaultValue, Type); }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    /// <summary>Rebuilds a designer parameter from a loaded core parameter (reverse of
    /// <see cref="ToReportParameter"/>) so a .repx round-trips every field.</summary>
    internal static DesignerParameter From(Reporting.Parameters.ReportParameter p)
        => new(p.Name, ClassifyClr(p.ValueType), ParameterValueConverter.Format(p.DefaultValue))
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
            AvailableValuesFilterField = p.AvailableValues?.FilterField,
            AvailableValuesDependsOn = p.AvailableValues?.DependsOn,
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
