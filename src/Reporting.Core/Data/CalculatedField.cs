namespace Reporting.Data;

/// <summary>RDL-compatible calculated field on a data source. Equivalent to a
/// <c>&lt;Field&gt;</c> with a <c>&lt;Value&gt;</c> child in Microsoft RDL — instead of
/// pulling the value from the underlying record set, the engine evaluates the expression
/// once per row using the current row context.</summary>
/// <remarks>
/// <para>Calculated fields appear in the expression context exactly like real fields:
/// <c>Fields.Total</c> works whether <c>Total</c> is a database column or a calculated
/// expression like <c>{Fields.UnitPrice * Fields.Quantity}</c>. The expression engine
/// re-evaluates the expression for each row, with access to other Fields, Parameters, and
/// Variables.</para>
///
/// <para>Recursion guard: a calculated field referencing itself (or forming a cycle through
/// other calculated fields) throws at evaluation time with a clear stack trace. The
/// expression context tracks the in-progress evaluation chain.</para>
/// </remarks>
/// <param name="Name">Field identifier — used as <c>Fields.{Name}</c> in expressions.</param>
/// <param name="Expression">Template/expression evaluated per row. Can reference other
/// <c>Fields.*</c>, <c>Parameters.*</c>, and aggregate functions.</param>
/// <param name="ResultType">Optional CLR type for the result — drives coercion. Defaults
/// to <see cref="string"/> when null (the expression result is left as-is).</param>
public sealed record CalculatedField(string Name, string Expression, Type? ResultType = null);
