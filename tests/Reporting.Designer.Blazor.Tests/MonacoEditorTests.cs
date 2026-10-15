using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The expression and SQL editors load Monaco from the copy packaged with the Designer unless the host points
/// elsewhere, and stay usable as a plain text box when Monaco cannot load — the expression is never lost behind an
/// empty editor.
/// </summary>
public sealed class MonacoEditorTests : BunitContext
{
    [Fact]
    public void Mount_SemOpcoes_DeixaOJsUsarACopiaEmpacotada()
    {
        var mount = JSInterop.SetupVoid("omniMonaco.mount", _ => true);
        mount.SetVoidResult();

        Render<MonacoEditor>(p => p.Add(x => x.InitialValue, "=1"));

        BaseUrlOf(mount.Invocations.Single()).Should().BeNull("null tells monaco-host.js to use the packaged copy");
    }

    [Fact]
    public void Mount_ComBaseUrlConfigurada_PassaAUrlAoJs()
    {
        Services.AddOmniReportMonaco(o => o.BaseUrl = "/lib/monaco/vs");
        var mount = JSInterop.SetupVoid("omniMonaco.mount", _ => true);
        mount.SetVoidResult();

        Render<MonacoEditor>();

        BaseUrlOf(mount.Invocations.Single()).Should().Be("/lib/monaco/vs");
    }

    [Fact]
    public void Mount_ComRotulo_PassaORotuloParaOEditorDoMonaco()
    {
        var mount = JSInterop.SetupVoid("omniMonaco.mount", _ => true);
        mount.SetVoidResult();

        Render<MonacoEditor>(p => p.Add(x => x.Label, "Consulta SQL"));

        var options = mount.Invocations.Single().Arguments[0]!;
        options.GetType().GetProperty("ariaLabel")!.GetValue(options).Should().Be("Consulta SQL",
            "screen readers name Monaco's own textarea, not the wrapper div");
    }

    [Fact]
    public async Task Fallback_EscritaProgramaticaSemAssinante_AtualizaACaixaDeTexto()
    {
        JSInterop.SetupVoid("omniMonaco.mount", _ => true).SetException(new JSException("Monaco não carregou"));
        var editor = Render<MonacoEditor>(p => p.Add(x => x.InitialValue, "=1"));
        editor.WaitForElement("textarea");

        await editor.InvokeAsync(() => editor.Instance.SetValueAsync("=Fields.B"));
        await editor.InvokeAsync(() => editor.Instance.InsertAsync(" * 2"));

        editor.Find("textarea").GetAttribute("value").Should().Be("=Fields.B * 2",
            "the box must show what GetValueAsync returns, or the next keystroke overwrites it");
    }

    [Fact]
    public void AddOmniReportMonaco_BaseUrlVazia_FalhaExplicando()
    {
        var services = new ServiceCollection();

        var act = () => services.AddOmniReportMonaco(o => o.BaseUrl = " ");

        act.Should().Throw<ArgumentException>().WithMessage("*deixe null*");
    }

    [Fact]
    public void Mount_MonacoNaoCarrega_ViraCaixaDeTextoComOValorInicial()
    {
        JSInterop.SetupVoid("omniMonaco.mount", _ => true).SetException(new JSException("Monaco não carregou"));

        var editor = Render<MonacoEditor>(p => p.Add(x => x.InitialValue, "{Fields.Total}").Add(x => x.Label, "Expressão"));

        editor.WaitForAssertion(() => editor.Find("textarea").GetAttribute("value").Should().Be("{Fields.Total}"));
        editor.Find("textarea").GetAttribute("aria-label").Should().Be("Expressão");
        editor.Find(".monaco-fallback-note").TextContent.Should().Contain("indisponível");
    }

    [Fact]
    public async Task Fallback_EdicaoEInsercao_NotificamEDevolvemOValor()
    {
        JSInterop.SetupVoid("omniMonaco.mount", _ => true).SetException(new JSException("Monaco não carregou"));
        var notified = new List<string>();
        var editor = Render<MonacoEditor>(p => p
            .Add(x => x.InitialValue, "=1")
            .Add(x => x.OnValueChanged, (string value) => notified.Add(value)));
        editor.WaitForElement("textarea");

        await editor.Find("textarea").InputAsync(new() { Value = "=Fields.A" });
        await editor.InvokeAsync(() => editor.Instance.InsertAsync(" + 1"));

        (await editor.InvokeAsync(() => editor.Instance.GetValueAsync())).Should().Be("=Fields.A + 1");
        notified.Should().Equal("=Fields.A", "=Fields.A + 1");
        JSInterop.Invocations.Select(i => i.Identifier).Should().NotContain("omniMonaco.insertAtCursor",
            "with Monaco absent the value is kept in .NET, not sent to a JS editor that does not exist");
    }

    private static string? BaseUrlOf(JSRuntimeInvocation invocation)
    {
        var options = invocation.Arguments[0]!;
        return (string?)options.GetType().GetProperty("baseUrl")!.GetValue(options);
    }
}
