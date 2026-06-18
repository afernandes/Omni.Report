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

    /// <summary>Culture used for formatting <c>{expr:format}</c> templates and numeric output.</summary>
    CultureInfo Culture { get; }

    /// <summary>Computes an aggregate (Sum, Avg, Count, Min, Max, RunningTotal).</summary>
    object? EvaluateAggregate(string function, string expression, AggregateScope scope);

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
}

/// <summary>Read-only string-keyed value lookup. Returns <c>null</c> for unknown keys.</summary>
public interface IValueLookup
{
    object? this[string key] { get; }
    bool Contains(string key);
    IEnumerable<string> Keys { get; }
}
