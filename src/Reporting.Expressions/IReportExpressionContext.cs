using System.Globalization;
using Reporting.Aggregates;

namespace Reporting.Expressions;

/// <summary>
/// Runtime context against which expressions are evaluated. Exposes the current row
/// (<see cref="Fields"/>), report-level <see cref="Parameters"/> and <see cref="Variables"/>,
/// paging state, and aggregate computation.
/// </summary>
public interface IReportExpressionContext
{
    /// <summary>Field values for the current data row.</summary>
    IValueLookup Fields { get; }

    /// <summary>Parameter values supplied to the report.</summary>
    IValueLookup Parameters { get; }

    /// <summary>Variable values (declared in the report definition).</summary>
    IValueLookup Variables { get; }

    /// <summary>Key of the innermost active group, or <c>null</c> outside any group.</summary>
    object? GroupKey { get; }

    /// <summary>1-based current page index.</summary>
    int PageNumber { get; }

    /// <summary>Total page count (resolved in the second pagination pass).</summary>
    int TotalPages { get; }

    /// <summary>The "wall clock" at report generation time.</summary>
    DateTime Now { get; }

    /// <summary>Date-only view of <see cref="Now"/>.</summary>
    DateTime Today { get; }

    /// <summary>Identifier of the user generating the report (configurable).</summary>
    string UserName { get; }

    /// <summary>Name of the report (RDL <c>Globals!ReportName</c>). Empty when unset.</summary>
    string ReportName { get; }

    /// <summary>Culture used for formatting <c>{expr:format}</c> templates and numeric output.</summary>
    CultureInfo Culture { get; }

    /// <summary>Computes an aggregate (Sum, Avg, Count, Min, Max, RunningTotal).</summary>
    object? EvaluateAggregate(string function, string expression, AggregateScope scope);

    /// <summary>SSRS-style cross-dataset lookup: scans the rows of <paramref name="datasetName"/> for ones
    /// whose <paramref name="destExpression"/> equals <paramref name="source"/> (evaluated in the caller's
    /// scope), returning <paramref name="resultExpression"/> from the matched row's scope. When
    /// <paramref name="all"/> is false returns the first match (or <c>null</c>); when true returns an
    /// <c>object?[]</c> of every match (LookupSet). Returns empty/null if the dataset isn't registered.</summary>
    object? EvaluateLookup(object? source, string destExpression, string resultExpression, string datasetName, bool all);

    /// <summary>Computes a positional function over the current row within a scope: <c>RowNumber</c>
    /// (1-based position), <c>CountRows</c> (rows in scope), or <c>Previous</c> (evaluates
    /// <paramref name="expression"/> against the prior row, or <c>null</c> on the first row).</summary>
    object? EvaluatePositional(string function, string expression, AggregateScope scope);

    /// <summary>Returns the current-row lookup of the data source named <paramref name="sourceName"/>,
    /// or <c>null</c> when the source is unknown OR has no current row in scope.</summary>
    /// <remarks>
    /// <para>Enables qualified field references in expressions:
    /// <c>{Fields.Clientes.Nome}</c> — when "Clientes" is a registered source, resolves to
    /// the cliente's "Nome" field on the parent record currently in scope.</para>
    /// <para>The unqualified form <c>{Fields.Nome}</c> continues to use the active band's
    /// primary source (no breaking changes).</para>
    /// </remarks>
    IValueLookup? GetSource(string sourceName);

    /// <summary>Attempts to resolve an unqualified field name by scanning every registered
    /// source's current row. Used as a fallback when <see cref="Fields"/> doesn't contain
    /// the field — typical in master-detail iteration where the live Fields is the child row
    /// but the user references parent fields without qualifying them
    /// (<c>{Fields.nome}</c> instead of <c>{Fields.Clientes.nome}</c>).</summary>
    /// <param name="fieldName">The unqualified field name.</param>
    /// <param name="value">The first matching value found. Search order: live Fields first,
    /// then every other source's current row in registration order.</param>
    /// <returns><c>true</c> if any source had the field, <c>false</c> otherwise.</returns>
    bool TryResolveUnqualifiedField(string fieldName, out object? value);

    /// <summary>RDL <c>ReportItems!Name.Value</c>: the value another named text box resolved to. The
    /// renderer records each named text box's value via <see cref="SetReportItem"/> as it renders, so a
    /// later band (e.g. a page footer referencing a body text box) can read it. Returns <c>null</c> for an
    /// unknown name or one not yet rendered (e.g. a page header referencing the body — that runs first).</summary>
    object? GetReportItem(string name);

    /// <summary>Records the rendered value of a named text box for <see cref="GetReportItem"/> lookups.</summary>
    void SetReportItem(string name, object? value);
}

/// <summary>Read-only string-keyed value lookup. Returns <c>null</c> for unknown keys.</summary>
public interface IValueLookup
{
    object? this[string key] { get; }
    bool Contains(string key);
    IEnumerable<string> Keys { get; }
}
