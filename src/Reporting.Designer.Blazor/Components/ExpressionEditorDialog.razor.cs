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
using Reporting.Printing;
using Reporting.Expressions;
namespace Reporting.Designer.Blazor.Components;

public partial class ExpressionEditorDialog
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public string InitialExpression { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = Array.Empty<DesignerDataSource>();
    [Parameter] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = Array.Empty<DesignerParameter>();
    [Parameter] public string Theme { get; set; } = "light";
    [Parameter] public EventCallback<string> OnApplied { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    private MonacoEditor? _monaco;
    private readonly string _monacoId = "expr-monaco-" + Guid.NewGuid().ToString("n")[..8];
    private string _value = string.Empty;
    private string _initialSnapshot = string.Empty;
    private string _filter = string.Empty;
    private bool _wasVisible;
    private ValidationResult? _validationResult;
    private EvalResult? _evalResult;
    private IReadOnlyList<MonacoCompletionItem> _completions = Array.Empty<MonacoCompletionItem>();
    private readonly HashSet<string> _expandedCategories = new() { "Fields", "Parameters", "Aggregates" };

    private static readonly ExpressionCompiler _compiler = new();
    private static readonly ExpressionEvaluator _evaluator = new();

    private static readonly FunctionEntry[] _aggregates =
    {
        new("Sum",           "Sum(Fields.X)",         "Soma os valores do campo no escopo corrente."),
        new("Avg",           "Avg(Fields.X)",         "Média aritmética dos valores."),
        new("Count",         "Count(Fields.X)",       "Quantos valores não-nulos existem."),
        new("Min",           "Min(Fields.X)",         "Menor valor do campo."),
        new("Max",           "Max(Fields.X)",         "Maior valor do campo."),
        new("RunningTotal",  "RunningTotal(Fields.X)","Soma acumulada linha-a-linha."),
        new("RunningValue",  "RunningValue(Fields.X, Sum)","Agregado acumulado (SSRS): expressão, função (Sum/Count/Avg/Min/Max) e escopo."),
    };

    private static readonly FunctionEntry[] _math =
    {
        new("Round",   "Round(value, digits)",    "Arredonda value para digits casas decimais."),
        new("Floor",   "Floor(value)",            "Arredonda para baixo."),
        new("Ceiling", "Ceiling(value)",          "Arredonda para cima."),
        new("Abs",     "Abs(value)",              "Valor absoluto."),
        new("Pow",     "Pow(base, exp)",          "base elevado a exp."),
        new("Sqrt",    "Sqrt(value)",             "Raiz quadrada."),
        new("Mod",     "value % divisor",         "Resto da divisão."),
    };

    private static readonly FunctionEntry[] _string =
    {
        new("Upper",     "Upper(text)",                       "Converte para MAIÚSCULAS."),
        new("Lower",     "Lower(text)",                       "Converte para minúsculas."),
        new("Trim",      "Trim(text)",                        "Remove espaços nas pontas."),
        new("Length",    "Length(text)",                      "Tamanho da string."),
        new("Substring", "Substring(text, start, length)",    "Extrai um trecho."),
        new("Replace",   "Replace(text, old, new)",           "Substitui ocorrências."),
        new("Concat",    "Concat(a, b)",                      "Concatena strings."),
        new("StartsWith","StartsWith(text, prefix)",          "Verifica prefixo."),
        new("EndsWith",  "EndsWith(text, suffix)",            "Verifica sufixo."),
        new("Contains",  "Contains(text, needle)",            "Verifica substring."),
    };

    private static readonly FunctionEntry[] _date =
    {
        new("Now",        "Now()",                       "Data + hora atual."),
        new("Today",      "Today()",                     "Data atual (sem hora)."),
        new("Year",       "Year(date)",                  "Extrai o ano."),
        new("Month",      "Month(date)",                 "Extrai o mês."),
        new("Day",        "Day(date)",                   "Extrai o dia."),
        new("Hour",       "Hour(date)",                  "Extrai a hora."),
        new("AddDays",    "AddDays(date, n)",            "Soma n dias à data."),
        new("AddMonths",  "AddMonths(date, n)",          "Soma n meses."),
        new("DateDiff",   "DateDiff('day', a, b)",       "Diferença entre datas."),
    };

    private static readonly FunctionEntry[] _logical =
    {
        new("IIF",   "IIF(condition, ifTrue, ifFalse)", "Operador ternário."),
        new("If",    "if(condition, a, b)",             "Equivalente ao IIF (lowercase)."),
        new("IsNull","IsNull(value)",                   "Verifica se é nulo."),
        new("Coalesce","Coalesce(a, b, c)",             "Retorna o primeiro não-nulo."),
        new("In",    "In(value, 'a', 'b', 'c')",        "Verifica pertinência."),
    };

    private static readonly FunctionEntry[] _conversion =
    {
        new("ToString",  "ToString(value)",       "Converte para texto."),
        new("ToDecimal", "ToDecimal(value)",      "Converte para decimal."),
        new("ToInt",     "ToInt(value)",          "Converte para inteiro."),
        new("ToDate",    "ToDate('yyyy-MM-dd')",  "Converte para DateTime."),
        new("Format",    "Format(value, 'C')",    "Formata com um spec (C/N/P/data...)."),
    };
    private static readonly OperatorEntry[] _operators =
    {
        new("+",   "+",   "Adição"),
        new("−",   "-",   "Subtração"),
        new("×",   "*",   "Multiplicação"),
        new("÷",   "/",   "Divisão"),
        new("%",   "%",   "Módulo"),
        new("=",   "==",  "Igual"),
        new("≠",   "!=",  "Diferente"),
        new("<",   "<",   "Menor"),
        new("≤",   "<=",  "Menor ou igual"),
        new(">",   ">",   "Maior"),
        new("≥",   ">=",  "Maior ou igual"),
        new("AND", " AND ", "E lógico"),
        new("OR",  " OR ",  "OU lógico"),
        new("NOT", "!",     "Negação"),
        new("( )", "()",    "Parênteses"),
        new("[ ]", "[]",    "Colchetes"),
    };
    private static readonly SnippetEntry[] _snippets =
    {
        new("Total geral (Sum no rodapé)",
            "{Sum(Fields.Total):C}",
            "{Sum(Fields.Total):C}",
            "Soma de todos os registros formatada como moeda."),
        new("Subtotal por grupo",
            "{Sum(Fields.Total, 'Group'):C}",
            "{Sum(Fields.Total, 'Group'):C}",
            "Soma apenas do grupo corrente."),
        new("Total acumulado por linha",
            "{RunningTotal(Fields.Total):C}",
            "{RunningTotal(Fields.Total):C}",
            "Saldo crescente — útil em extratos."),
        new("Página X de Y",
            "Página {PageNumber} de {TotalPages}",
            "Página {PageNumber} de {TotalPages}",
            "Numeração de páginas no rodapé."),
        new("Condicional simples",
            "{IIF(Fields.Total > 1000, 'Alto', 'Baixo')}",
            "{IIF(Fields.Total > 1000, 'Alto', 'Baixo')}",
            "Ternário — valor depende da condição."),
        new("Total com tributos (Lei 12.741)",
            "Tributos: {Sum(Fields.Total) * 0.18:C}",
            "{Sum(Fields.Total) * 0.18:C}",
            "Cálculo aproximado de tributos."),
        new("Data emissão pt-BR",
            "Emitido em {Now():dd/MM/yyyy HH:mm}",
            "{Now():dd/MM/yyyy HH:mm}",
            "Formato brasileiro de data/hora."),
        new("Nome em maiúsculas",
            "{Upper(Fields.Cliente)}",
            "{Upper(Fields.Cliente)}",
            "Conversão para caixa alta."),
        new("Concatenação de campos",
            "{Concat(Fields.Cidade, ' / ', Fields.UF)}",
            "{Concat(Fields.Cidade, ' / ', Fields.UF)}",
            "Junta dois campos com separador."),
    };

    // ─── Lifecycle ─────────────────────────────────────────────────────────────
    protected override void OnParametersSet()
    {
        // Rising-edge only — only initialize when the dialog OPENS so user input
        // isn't wiped by parent re-renders during typing.
        if (Visible && !_wasVisible)
        {
            _initialSnapshot = InitialExpression ?? string.Empty;
            _value = _initialSnapshot;
            _validationResult = null;
            _evalResult = null;
            _filter = string.Empty;
            _completions = BuildCompletions();
        }
        _wasVisible = Visible;
    }

    private IReadOnlyList<MonacoCompletionItem> BuildCompletions()
    {
        var list = new List<MonacoCompletionItem>();
        foreach (var ds in DataSources)
        {
            foreach (var f in ds.Fields)
            {
                list.Add(new MonacoCompletionItem
                {
                    Label = $"Fields.{f.Name}",
                    Kind = "field",
                    InsertText = $"Fields.{f.Name}",
                    Detail = $"{f.Type} · {ds.Name}",
                    Documentation = f.Sample is null ? null : $"**Amostra:** `{f.Sample}`",
                });
            }
        }
        foreach (var p in Parameters)
        {
            list.Add(new MonacoCompletionItem
            {
                Label = $"Parameters.{p.Name}",
                Kind = "parameter",
                InsertText = $"Parameters.{p.Name}",
                Detail = $"{p.Type} · default: {p.DefaultValue ?? "—"}",
            });
        }
        foreach (var fn in _aggregates.Concat(_math).Concat(_string).Concat(_date).Concat(_logical).Concat(_conversion))
        {
            list.Add(new MonacoCompletionItem
            {
                Label = fn.Insert,
                Kind = "function",
                InsertText = fn.Insert.Replace("(", "(${1:").Replace(")", "})"),
                Snippet = true,
                Detail = fn.Name,
                Documentation = fn.Description,
            });
        }
        return list;
    }

    // ─── Catalog rendering ─────────────────────────────────────────────────────
    private (string Name, string Insert, string? Tooltip)[] AllFields()
        => DataSources.SelectMany(ds =>
            ds.Fields.Select(f => ($"Fields.{f.Name}", $"Fields.{f.Name}", (string?)$"{ds.Name} · {f.Type}{(f.Sample is null ? "" : " — " + f.Sample)}"))
        ).ToArray();

    private (string Name, string Insert, string? Tooltip)[] AllParameters()
        => Parameters.Select(p =>
            ($"Parameters.{p.Name}", $"Parameters.{p.Name}", (string?)$"{p.Type} · default: {p.DefaultValue ?? "—"}")
        ).ToArray();

    private RenderFragment RenderCategory(
        string category,
        (string Name, string Insert, string? Tooltip)[] items,
        bool defaultExpanded,
        string kind) => builder =>
    {
        // When filtering, force-expand every category so matches are visible.
        var expanded = !string.IsNullOrEmpty(_filter) || _expandedCategories.Contains(category);

        var filtered = string.IsNullOrEmpty(_filter)
            ? items
            : items.Where(i => i.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (!string.IsNullOrEmpty(_filter) && filtered.Length == 0)
        {
            return;
        }
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "expr-category");
        {
            // Header
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "type", "button");
            builder.AddAttribute(4, "style", "display:grid;grid-template-columns:14px 1fr auto;align-items:center;gap:4px;width:100%;text-align:left;padding:6px 8px;background:transparent;border:0;cursor:pointer;font-size:11px;font-weight:600;color:var(--text-tertiary);letter-spacing:var(--tracked);text-transform:uppercase;");
            builder.AddAttribute(5, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create(
                this, () =>
                {
                    if (_expandedCategories.Contains(category))
                    {
                        _expandedCategories.Remove(category);
                    }
                    else
                    {
                        _expandedCategories.Add(category);
                    }
                }));
            // Each of the 3 grid columns needs its OWN element. The chevron + name were adjacent text
            // nodes that merged into a SINGLE grid item (crammed into the 14px column, overflowing on top
            // of the count). Wrap each so they land in their own column.
            builder.OpenElement(6, "span");
            builder.AddContent(7, expanded ? "▾" : "▸");
            builder.CloseElement();

            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "style", "overflow:hidden;text-overflow:ellipsis;white-space:nowrap;");
            builder.AddContent(10, category);
            builder.CloseElement();

            builder.OpenElement(11, "span");
            builder.AddAttribute(12, "style", "font-family:var(--font-mono);font-size:10px;color:var(--text-tertiary);text-transform:none;letter-spacing:0;");
            builder.AddContent(13, filtered.Length.ToString());
            builder.CloseElement();

            builder.CloseElement(); // button

            // Items
            if (expanded)
            {
                foreach (var item in filtered)
                {
                    builder.OpenElement(14, "button");
                    builder.AddAttribute(15, "type", "button");
                    builder.AddAttribute(16, "title", item.Tooltip ?? string.Empty);
                    var colorVar = kind == "field" ? "var(--data-bound)"
                                  : kind == "parameter" ? "var(--syn-number)"
                                  : "var(--syn-function)";
                    builder.AddAttribute(17, "style",
                        $"display:block;width:100%;text-align:left;padding:3px 22px;font-family:var(--font-mono);font-size:11px;border:0;background:transparent;cursor:pointer;color:{colorVar};line-height:1.5;");
                    var insert = item.Insert;
                    builder.AddAttribute(18, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create(
                        this, () => Insert(insert)));
                    builder.AddContent(19, item.Name);
                    builder.CloseElement();
                }
            }
        }
        builder.CloseElement();
    };

    // ─── Editor interactions ───────────────────────────────────────────────────
    private async void Insert(string token)
    {
        if (_monaco is not null)
        {
            await _monaco.InsertAsync(token);
        }
        _validationResult = null;
    }

    private async void InsertSnippet(string template)
    {
        if (_monaco is not null)
        {
            await _monaco.SetValueAsync(template);
        }
        _validationResult = null;
    }

    private void OnEditorChanged(string value)
    {
        _value = value;
        if (_validationResult is not null)
        {
            _validationResult = null;
        }
        if (_evalResult is not null)
        {
            _evalResult = null;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_value))
        {
            _validationResult = new ValidationResult(true, "Expressão vazia.");
            return;
        }
        try
        {
            if (_value.Contains('{'))
            {
                int open = _value.Count(c => c == '{');
                int close = _value.Count(c => c == '}');
                if (open != close)
                {
                    _validationResult = new ValidationResult(false, $"Chaves desbalanceadas: {open} '{{' vs {close} '}}'.");
                    return;
                }
                _validationResult = new ValidationResult(true, "Template balanceado.");
            }
            else
            {
                _compiler.Compile(_value);
                _validationResult = new ValidationResult(true, "Expressão sintaticamente válida.");
            }
        }
        catch (Exception ex)
        {
            _validationResult = new ValidationResult(false, ex.Message);
        }
    }

    private void OnEvaluate()
    {
        // Build a sample context from the first data source's first sample row.
        try
        {
            var ctx = new ReportExpressionContext();
            var row = new Dictionary<string, object?>();
            foreach (var ds in DataSources)
            {
                foreach (var f in ds.Fields)
                {
                    row[f.Name] = SampleValueFor(f);
                }
            }
            ctx.SetCurrentRow(row);
            foreach (var p in Parameters)
            {
                ctx.ParametersStore.Set(p.Name, p.DefaultValue ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(_value))
            {
                _evalResult = new EvalResult("(vazio)", false);
                return;
            }

            // Templates vs raw expression
            if (_value.Contains('{'))
            {
                var tpl = new TemplateRenderer(_evaluator);
                var output = tpl.Render(_value, ctx);
                _evalResult = new EvalResult(output, false);
            }
            else
            {
                var compiled = _compiler.Compile(_value);
                var raw = _evaluator.Evaluate(_value, ctx);
                _evalResult = new EvalResult(raw?.ToString() ?? "(null)", false);
            }
        }
        catch (Exception ex)
        {
            _evalResult = new EvalResult($"⚠ {ex.Message}", true);
        }
    }

    private static object? SampleValueFor(DesignerField f) => f.Type switch
    {
        DesignerFieldType.Number => 42m,
        DesignerFieldType.Money => 1234.56m,
        DesignerFieldType.Date => DateTimeOffset.UtcNow.UtcDateTime,
        DesignerFieldType.Bool => true,
        _ => f.Sample ?? f.Name,
    };

    private async Task OnApply()
    {
        if (_monaco is not null)
        {
            var live = await _monaco.GetValueAsync();
            if (live is not null)
            {
                _value = live;
            }
        }
        await OnApplied.InvokeAsync(_value);
    }

    private Task OnCancel() => OnClosed.InvokeAsync();
}
