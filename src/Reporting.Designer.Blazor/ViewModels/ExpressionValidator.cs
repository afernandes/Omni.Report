using System.Text.RegularExpressions;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Lightweight design-time validator for element <c>Expression</c> templates.
/// Catches the two errors a user is overwhelmingly likely to make at edit time:
///   • Unbalanced braces (<c>{Foo</c> or <c>}}</c> alone).
///   • References to non-existent fields or parameters.
/// The runtime <c>Reporting.Expressions.TemplateRenderer</c> is the source of truth for
/// actual rendering; this validator only surfaces problems early in the designer so the
/// user doesn't have to enter Preview mode to discover them.</summary>
public static class ExpressionValidator
{
    private static readonly Regex FieldRef = new(
        @"\bFields\s*\.\s*([A-Za-z_][A-Za-z0-9_\.]*)",
        RegexOptions.Compiled);

    private static readonly Regex ParamRef = new(
        @"\bParameters\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    /// <summary>Returns <c>null</c> when the template is valid, otherwise a short Portuguese
    /// error message ready to render in the property grid / underline tooltip.</summary>
    public static string? Validate(
        string? expression,
        IEnumerable<DesignerDataSource> dataSources,
        IEnumerable<DesignerParameter> parameters)
    {
        if (string.IsNullOrEmpty(expression)) return null;

        // Walk the template once, validating brace balance and recording each placeholder.
        var placeholders = new List<string>();
        var i = 0;
        while (i < expression.Length)
        {
            var c = expression[i];
            if (c == '{')
            {
                // Escaped {{
                if (i + 1 < expression.Length && expression[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }
                var end = FindClosingBrace(expression, i + 1);
                if (end < 0) return "Chave '{' sem fechamento correspondente.";
                var inner = expression.Substring(i + 1, end - i - 1);
                if (inner.Length == 0) return "Placeholder '{}' vazio.";

                var colon = inner.IndexOf(':');
                var exprText = (colon >= 0 ? inner[..colon] : inner).Trim();
                if (exprText.Length == 0) return "Expressão vazia antes do ':'.";

                placeholders.Add(exprText);
                i = end + 1;
            }
            else if (c == '}')
            {
                if (i + 1 < expression.Length && expression[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }
                return "Fechamento '}' sem abertura correspondente.";
            }
            else i++;
        }

        // No placeholders found? The whole template is treated as a single expression by
        // some host code paths — validate it too (so `Fields.Foo` typed bare reports an
        // error rather than silently rendering as literal text).
        if (placeholders.Count == 0)
        {
            // Heuristic: only validate as expression if it looks like one
            // (contains a Fields./Parameters. token). Otherwise it's literal text.
            if (FieldRef.IsMatch(expression) || ParamRef.IsMatch(expression))
            {
                return ValidateReferences(expression, dataSources, parameters);
            }
            return null;
        }

        foreach (var ph in placeholders)
        {
            var err = ValidateReferences(ph, dataSources, parameters);
            if (err is not null) return err;
        }
        return null;
    }

    private static string? ValidateReferences(
        string expr,
        IEnumerable<DesignerDataSource> dataSources,
        IEnumerable<DesignerParameter> parameters)
    {
        var sourceList = dataSources.ToList();
        var sourceNames = sourceList.Select(ds => ds.Name).ToHashSet(StringComparer.Ordinal);
        // Multimap: fieldName → list of sources containing it. Used both for membership
        // check (unqualified) and ambiguity detection.
        var fieldOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var ds in sourceList)
        {
            foreach (var f in ds.Fields)
            {
                if (!fieldOwners.TryGetValue(f.Name, out var list))
                {
                    list = new List<string>(2);
                    fieldOwners[f.Name] = list;
                }
                list.Add(ds.Name);
            }
        }
        var paramNames = parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (Match m in FieldRef.Matches(expr))
        {
            var path = m.Groups[1].Value;
            var dot = path.IndexOf('.');
            var head = dot < 0 ? path : path[..dot];
            var tail = dot < 0 ? null : path[(dot + 1)..];

            // Case 1: qualified form Fields.SourceName.Field — head is a source name.
            if (tail is not null && sourceNames.Contains(head))
            {
                var ds = sourceList.First(x => x.Name == head);
                var tailHead = tail.Contains('.') ? tail[..tail.IndexOf('.')] : tail;
                if (!ds.Fields.Any(f => f.Name == tailHead))
                {
                    return $"Campo '{tailHead}' não existe na fonte '{head}'. Use {{Fields.{head}.<campo>}}.";
                }
                continue;
            }

            // Case 2: unqualified — must exist in at least one source. If it lives in more
            // than one, flag the ambiguity so the user uses the qualified form.
            if (fieldOwners.TryGetValue(head, out var owners))
            {
                if (owners.Count > 1)
                {
                    return $"Campo '{head}' existe em {owners.Count} fontes ({string.Join(", ", owners)}). " +
                           $"Use a forma qualificada {{Fields.<Fonte>.{head}}}.";
                }
                continue;
            }

            // Case 3: nested member access on a single field (e.g. {Fields.Endereco.Rua} when
            // Endereco is itself an object on the row). The dotted head can survive when the
            // full path was registered as a field name.
            if (fieldOwners.ContainsKey(path)) continue;

            return $"Campo desconhecido: 'Fields.{path}'.";
        }

        foreach (Match m in ParamRef.Matches(expr))
        {
            var name = m.Groups[1].Value;
            if (!paramNames.Contains(name))
            {
                return $"Parâmetro desconhecido: 'Parameters.{name}'.";
            }
        }
        return null;
    }

    private static int FindClosingBrace(string s, int from)
    {
        var depth = 1;
        for (var i = from; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }
}
