namespace Reporting.Expressions.Roslyn;

/// <summary>Thrown when an RDL <c>Code</c> block fails to compile.</summary>
public sealed class RoslynCodeCompilationException : Exception
{
    public RoslynCodeCompilationException(string message)
        : base("Failed to compile report Code block: " + message)
    {
    }
}
