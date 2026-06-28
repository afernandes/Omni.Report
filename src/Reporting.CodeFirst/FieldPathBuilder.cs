using System.Linq.Expressions;

namespace Reporting.CodeFirst;

/// <summary>
/// Converts a typed lambda such as <c>v =&gt; v.Cliente.Nome</c> into the corresponding
/// report expression string <c>Fields.Cliente.Nome</c>.
/// </summary>
public static class FieldPathBuilder
{
    /// <summary>Converts a member-access lambda over <typeparamref name="T"/> into its report
    /// expression string, prefixing the property path with <c>Fields.</c>.</summary>
    /// <param name="selector">A simple member-access chain rooted at the lambda parameter, e.g.
    /// <c>v =&gt; v.Cliente.Nome</c>.</param>
    /// <returns>The report expression, e.g. <c>Fields.Cliente.Nome</c>.</returns>
    /// <exception cref="ArgumentException">The selector is not a member-access chain rooted at the
    /// parameter, or accesses no property.</exception>
    public static string From<T>(Expression<Func<T, object>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var body = selector.Body;
        // Strip the `(object)x` boxing convert that the compiler inserts for value-returning lambdas.
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } u)
        {
            body = u.Operand;
        }
        var parts = new Stack<string>();
        while (body is MemberExpression member)
        {
            parts.Push(member.Member.Name);
            body = member.Expression!;
        }
        if (body is not ParameterExpression)
        {
            throw new ArgumentException(
                "Selector must be a simple member access chain rooted at the lambda parameter, e.g. v => v.Cliente or v => v.Cliente.Nome.",
                nameof(selector));
        }
        if (parts.Count == 0)
        {
            throw new ArgumentException("Selector must access at least one property.", nameof(selector));
        }
        return "Fields." + string.Join(".", parts);
    }
}
