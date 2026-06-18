using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Reporting.Expressions.Roslyn;

/// <summary>
/// Compiles an RDL <c>Code</c> block (a set of C# helper methods) into an in-memory assembly via
/// Roslyn and exposes a resolver suitable for
/// <see cref="ExpressionEvaluator.CodeFunctionResolver"/>, so expressions can call
/// <c>Code.MethodName(...)</c>.
/// </summary>
/// <remarks>
/// <para><b>Security:</b> this executes arbitrary C# embedded in a report definition. Only enable
/// it for report sources you trust — a malicious <c>.repx</c> could run any code with the host's
/// privileges. It is opt-in by design: the core engine never references this package.</para>
/// <para>The compiled assembly is cached for this evaluator's lifetime. For a server rendering
/// many distinct report definitions, create one evaluator per definition (e.g. keyed by a hash of
/// the code) so assemblies don't accumulate; a collectible <c>AssemblyLoadContext</c> is a future
/// refinement.</para>
/// </remarks>
public sealed class RoslynCodeEvaluator
{
    private readonly object? _instance;
    private readonly Dictionary<string, MethodInfo> _methods;

    public RoslynCodeEvaluator(string source, IEnumerable<Assembly>? additionalReferences = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        // Wrap the user's method declarations in a class so they become callable members.
        var wrapped =
            "using System;\n" +
            "using System.Collections.Generic;\n" +
            "using System.Globalization;\n" +
            "using System.Linq;\n" +
            "using System.Text;\n" +
            "public sealed class __ReportCode\n{\n" + source + "\n}\n";

        var tree = CSharpSyntaxTree.ParseText(wrapped);
        var compilation = CSharpCompilation.Create(
            assemblyName: "OmniReport.Code." + Guid.NewGuid().ToString("N"),
            syntaxTrees: [tree],
            references: BuildReferences(additionalReferences),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage(CultureInfo.InvariantCulture)));
            throw new RoslynCodeCompilationException(errors);
        }

        var assembly = Assembly.Load(ms.ToArray());
        var type = assembly.GetType("__ReportCode")
                   ?? throw new RoslynCodeCompilationException("Compiled code class '__ReportCode' was not found.");

        _instance = type.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(type) : null;
        _methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.DeclaringType == type && !m.IsSpecialName)
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>Resolver shape for <see cref="ExpressionEvaluator.CodeFunctionResolver"/> —
    /// invokes the compiled method named <paramref name="methodName"/> with coerced arguments.
    /// Returns <c>null</c> when the method doesn't exist.</summary>
    public object? Invoke(string methodName, object?[] args)
    {
        if (!_methods.TryGetValue(methodName, out var method))
        {
            return null;
        }
        var parameters = method.GetParameters();
        var coerced = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var supplied = args is not null && i < args.Length ? args[i] : null;
            coerced[i] = Coerce(supplied, parameters[i].ParameterType);
        }
        return method.Invoke(method.IsStatic ? null : _instance, coerced);
    }

    private static object? Coerce(object? value, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (value is null)
        {
            return underlying.IsValueType && Nullable.GetUnderlyingType(target) is null
                ? Activator.CreateInstance(underlying)
                : null;
        }
        if (underlying.IsInstanceOfType(value))
        {
            return value;
        }
        try
        {
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException) { }
        catch (FormatException) { }
        catch (OverflowException) { }
        return underlying.IsValueType ? Activator.CreateInstance(underlying) : null;
    }

    private static List<MetadataReference> BuildReferences(IEnumerable<Assembly>? additional)
    {
        var refs = new List<MetadataReference>();
        // Reference every trusted platform assembly so the compiled code can use the full BCL.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (var path in tpa.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }
                TryAddReference(refs, path);
            }
        }
        if (additional is not null)
        {
            foreach (var asm in additional)
            {
                if (!string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
                {
                    TryAddReference(refs, asm.Location);
                }
            }
        }
        return refs;
    }

    private static void TryAddReference(List<MetadataReference> refs, string path)
    {
        try
        {
            refs.Add(MetadataReference.CreateFromFile(path));
        }
        catch (IOException) { }
        catch (BadImageFormatException) { }
        catch (ArgumentException) { }
    }
}

/// <summary>Thrown when an RDL <c>Code</c> block fails to compile.</summary>
public sealed class RoslynCodeCompilationException : Exception
{
    public RoslynCodeCompilationException(string message)
        : base("Failed to compile report Code block: " + message)
    {
    }
}
