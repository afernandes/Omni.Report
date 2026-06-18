using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reporting.DataSources;
using Reporting.Hosting;
using Reporting.Layout;
using Reporting.Output.Excel;
using Reporting.Output.Pdf;
using Reporting.Printing;
using Reporting.Rendering;
using Xunit;

namespace Reporting.Hosting.AspNetCore.Tests;

public class ReportingBuilderTests
{
    [Fact]
    public void AddReporting_with_defaults_registers_skia_pdf_excel_and_paginator()
    {
        var services = new ServiceCollection();
        services.AddReporting();

        var provider = services.BuildServiceProvider();

        // Defaults: Skia rendering + PDF + Excel.
        provider.GetService<IRenderingContext>().Should().NotBeNull("default UseSkiaRendering should register IRenderingContext");
        provider.GetService<ITextMeasurer>().Should().NotBeNull();
        provider.GetService<SkiaPdfExporter>().Should().NotBeNull();
        provider.GetService<IReportExporter>().Should().NotBeNull("SkiaPdfExporter is registered as IReportExporter");
        provider.GetService<ExcelExporter>().Should().NotBeNull();

        // Paginator + serializer wired by the constructor.
        provider.GetService<IReportPaginator>().Should().NotBeNull();
        provider.GetService<DataSourceRegistry>().Should().NotBeNull();
    }

    [Fact]
    public void AddReporting_returns_the_same_IServiceCollection_for_chaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddReporting();
        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddDataSource_inmemory_registers_with_DataSourceRegistry()
    {
        var services = new ServiceCollection();
        var sample = new[] { new { Id = 1, Name = "A" }, new { Id = 2, Name = "B" } };

        services.AddReporting(opts => opts.AddDataSource("Items", sample));

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<DataSourceRegistry>();
        var ds = registry.Get("Items");
        ds.Should().NotBeNull();
        ds!.Name.Should().Be("Items");
    }

    [Fact]
    public void AddDataSource_custom_source_registers_with_registry()
    {
        var services = new ServiceCollection();
        var stub = new StubDataSource("Stub");

        services.AddReporting(opts => opts.AddDataSource(stub));

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<DataSourceRegistry>();
        registry.Get("Stub").Should().BeSameAs(stub);
    }

    [Fact]
    public void UsePrinter_instance_registers_singleton()
    {
        var services = new ServiceCollection();
        var fake = new FakePrinter();

        services.AddReporting(opts => opts.UsePrinter(fake));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IReportPrinter>().Should().BeSameAs(fake);
    }

    [Fact]
    public void UsePrinter_generic_resolves_concrete_type_from_DI()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FakePrinter>(); // satisfies TPrinter constructor
        services.AddReporting(opts => opts.UsePrinter<FakePrinter>());

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IReportPrinter>().Should().BeOfType<FakePrinter>();
    }

    [Fact]
    public void Custom_PdfExportOptions_persist_in_DI()
    {
        var services = new ServiceCollection();
        var opts = new PdfExportOptions { Title = "Custom" };

        services.AddReporting(b => b.UsePdfOutput(opts));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PdfExportOptions>().Title.Should().Be("Custom");
    }

    [Fact]
    public void Custom_ExcelExportOptions_persist_in_DI()
    {
        var services = new ServiceCollection();
        var opts = new ExcelExportOptions { Title = "Vendas" };

        services.AddReporting(b => b.UseExcelOutput(opts));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ExcelExportOptions>().Title.Should().Be("Vendas");
    }

    [Fact]
    public void Configure_chain_invokes_in_order()
    {
        var services = new ServiceCollection();
        services.AddReporting(b => b
            .UseSkiaRendering(dpi: 144f)
            .UsePdfOutput()
            .UseExcelOutput()
            .AddDataSource("X", new[] { 1, 2, 3 }));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DataSourceRegistry>().Get("X").Should().NotBeNull();
        provider.GetRequiredService<SkiaPdfExporter>().Should().NotBeNull();
        provider.GetRequiredService<ExcelExporter>().Should().NotBeNull();
    }

    [Fact]
    public void AddReporting_throws_on_null_services()
    {
        IServiceCollection? services = null;
        FluentActions.Invoking(() => services!.AddReporting())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReportingBuilder_throws_on_null_services_arg()
    {
        FluentActions.Invoking(() => new ReportingBuilder(null!))
            .Should().Throw<ArgumentNullException>();
    }

    private sealed class StubDataSource : IReportDataSource
    {
        public StubDataSource(string name) { Name = name; }
        public string Name { get; }
        public IReportRecordSchema Schema { get; } = new EmptySchema();
        public IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken ct = default)
            => AsyncEnumerable.Empty<IReportRecord>();

        private sealed class EmptySchema : IReportRecordSchema
        {
            public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
            public int IndexOf(string name) => -1;
        }
    }

    private sealed class FakePrinter : IReportPrinter
    {
        public string Driver => "Fake";
        public Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PrinterInfo>>(Array.Empty<PrinterInfo>());
        public Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken ct = default)
            => Task.FromResult(new PrinterCapabilities(printerName,
                Reporting.Common.EquatableArray<Reporting.Paper.PaperSize>.Empty,
                Reporting.Common.EquatableArray<string>.Empty,
                SupportsDuplex: false, SupportsColor: false));
        public Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PrintResult(true, report.PageCount));
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
