using NCalc;
using System.Text.RegularExpressions;
namespace Reporting.Expressions;

/// <summary>Only constants, scalar fields and operators are safe to accumulate across row snapshots.</summary>
internal static class RowOnlyExpression
{
    internal static bool IsEligible(LogicalExpression expression)
    {
        var pending = new Stack<LogicalExpression>();
        pending.Push(expression);
        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case ValueExpression: break;
                case Identifier identifier:
                    if (!Regex.IsMatch(identifier.Name, @"^Fields[!.][\p{L}_][\p{L}\p{N}_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
                    break;
                case BinaryExpression binary:
                    pending.Push(binary.LeftExpression); pending.Push(binary.RightExpression); break;
                case UnaryExpression unary:
                    pending.Push(unary.Expression); break;
                case TernaryExpression ternary:
                    pending.Push(ternary.LeftExpression); pending.Push(ternary.MiddleExpression); pending.Push(ternary.RightExpression); break;
                default: return false;
            }
        }
        return true;
    }
}
