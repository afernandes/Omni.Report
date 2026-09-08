using System.Reflection;
using Bunit;
using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;
namespace Reporting.Designer.Blazor.Tests;

public sealed class PreviewDocumentIsolationTests : BunitContext
{
    [Fact]
    public async Task OnPreview_TrocaAbaDuranteLeitura_NaoPublicaPreviewNoNovoDocumento()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var state = new DesignerState();
        state.Parameters.Clear(); state.DataSources.Clear();
        state.DataSources.Add(new DesignerDataSource("Dados", []));
        var source = new GatedPreviewSource(new EnumerableDataSource<string>("Dados", ["A"]));
        state.PreviewDataRegistry = new DataSourceRegistry(); state.PreviewDataRegistry.Register(source);
        var component = Render<ReportDesigner>(parameters => parameters.Add(d => d.InitialState, state));
        Task? pending = null;
        await component.InvokeAsync(() => { pending = (Task)typeof(ReportDesigner).GetMethod("OnPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component.Instance, null)!; });
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() => { state.OpenNewDocument("Outro"); });
        source.Release.TrySetResult();
        await pending!.WaitAsync(TimeSpan.FromSeconds(5));
        state.IsPreviewing.Should().BeFalse();
        component.Markup.Should().NotContain("alt=\"Página 1");
    }
}
