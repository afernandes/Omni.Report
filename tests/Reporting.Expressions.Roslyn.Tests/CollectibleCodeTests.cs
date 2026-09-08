using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Xunit;
namespace Reporting.Expressions.Roslyn.Tests;

public sealed class CollectibleCodeTests
{
    [Fact]
    public void Dispose_VinteCompilacoes_LiberaContextosColetaveis()
    {
        var references = Enumerable.Range(0, 20).Select(_ => CompileAndRelease()).ToArray();
        for (int attempt = 0; attempt < 10 && references.Any(reference => reference.IsAlive); attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        references.Should().OnlyContain(reference => !reference.IsAlive);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompileAndRelease()
    {
        using var evaluator = new RoslynCodeEvaluator("public int Valor() => 42;");
        evaluator.Invoke("Valor", []).Should().Be(42);
        var context = (AssemblyLoadContext)typeof(RoslynCodeEvaluator).GetField("_loadContext", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(evaluator)!;
        context.IsCollectible.Should().BeTrue();
        return new WeakReference(context);
    }
    [Fact]
    public void Invoke_AposDispose_FalhaExplicitamente()
    {
        var evaluator = new RoslynCodeEvaluator("public int Valor() => 1;");
        evaluator.Dispose();
        evaluator.Dispose();
        var action = () => evaluator.Invoke("Valor", []);
        action.Should().Throw<ObjectDisposedException>();
    }
}
