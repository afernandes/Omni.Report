using System.Diagnostics;
using System.Text.Json;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering.Skia;

var paginator = new ReportPaginator();
var outputs = new List<object>();
string[] modes = ["buffered", "prepared", "stream", "preview-all", "preview-window"];
foreach (var mode in modes) await Execute(mode, 1, 0);
foreach (int pages in new[] { 1, 10, 100 })
{
foreach (int concurrency in new[] { 1, 5 })
{
foreach (var mode in modes)
{
for (int repetition = 1; repetition <= 3; repetition++)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    using var process = Process.GetCurrentProcess();
    process.Refresh();
    long privateBefore = process.PrivateMemorySize64;
    long managedBefore = GC.GetTotalMemory(false);
    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    int[] gcBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    long privatePeak = privateBefore, workingPeak = process.WorkingSet64, heapPeak = managedBefore;
    uint gdiPeak = GuiResources.GetGuiResources(process.Handle, 0), userPeak = GuiResources.GetGuiResources(process.Handle, 1);
    using var sampling = new CancellationTokenSource();
    var sampler = Task.Run(async () =>
    {
        try
        {
            while (!sampling.IsCancellationRequested)
            {
                process.Refresh();
                privatePeak = Math.Max(privatePeak, process.PrivateMemorySize64);
                workingPeak = Math.Max(workingPeak, process.WorkingSet64);
                heapPeak = Math.Max(heapPeak, GC.GetTotalMemory(false));
                gdiPeak = Math.Max(gdiPeak, GuiResources.GetGuiResources(process.Handle, 0));
                userPeak = Math.Max(userPeak, GuiResources.GetGuiResources(process.Handle, 1));
                await Task.Delay(20, sampling.Token);
            }
        }
        catch (OperationCanceledException) when (sampling.IsCancellationRequested) { }
    });
    var watch = Stopwatch.StartNew();
    var reports = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(index => Task.Run(() => Execute(mode, pages, index))));
    double elapsed = watch.Elapsed.TotalMilliseconds;
    await sampling.CancelAsync(); await sampler;
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    int[] collections = [GC.CollectionCount(0) - gcBefore[0], GC.CollectionCount(1) - gcBefore[1], GC.CollectionCount(2) - gcBefore[2]];
    var info = GC.GetGCMemoryInfo();
    long loh = info.GenerationInfo.Length > 3 ? info.GenerationInfo[3].SizeAfterBytes : 0;
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    process.Refresh();
    var result = new { mode, pages, concurrency, repetition, elapsedMs = elapsed,
        pagesPerSecond = pages * concurrency / (elapsed / 1000), allocatedBytes = allocated, collections,
        managedBefore, managedPeakSampled = heapPeak, managedAfterGc = GC.GetTotalMemory(false),
        privateBefore, privatePeakSampled = privatePeak, privateAfterGc = process.PrivateMemorySize64,
        workingPeakSampled = workingPeak, lohAfterLastObservedGc = loh, gdiPeak, userPeak, reports };
    outputs.Add(result);
    Console.WriteLine(JsonSerializer.Serialize(result));
}
}
}
}
File.WriteAllText(args[0], JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow,
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, dpi = 96, warmup = "one report per mode", sampleIntervalMs = 20, results = outputs }, new JsonSerializerOptions { WriteIndented = true }));

async Task<object> Execute(string mode, int pages, int session)
{
    var registry = new DataSourceRegistry();
    registry.Register(new EnumerableDataSource<Registro>("Dados", Enumerable.Range(0, pages).Select(index => new Registro(index + 1m))));
    var definition = ReportDefinition.Empty("Medição") with
    {
        Parameters = EquatableArray.Create(new Reporting.Parameters.ReportParameter("Deslocamento", typeof(decimal))),
        PageSetup = new PageSetup(new PaperSize("Medição", 210.Mm(), 100.Mm())),
        DataSources = EquatableArray.Create(new DataSourceDefinition("Dados")),
        Detail = new DetailBand(100.Mm(), new EquatableArray<ReportElement>(Enumerable.Range(0, 30).Select(index => new TextBoxElement
        {
            Id = "valor-" + index, Bounds = new Rectangle(2.Mm(), (index * 3).Mm(), 180.Mm(), 3.Mm()),
            Expression = "Fields.Valor + Parameters.Deslocamento"
        })))
    };
    var request = new PaginationRequest { Definition = definition, DataSources = registry,
        Parameters = new Dictionary<string, object?> { ["Deslocamento"] = session * 1000m },
        Limits = new() { MaxSourceRows = 10_000, MaxIterationRows = 20_000, MaxPages = 1000, MaxPrimitives = 100_000 } };
    var watch = Stopwatch.StartNew();
    double firstPageMs = 0; int actualPages = 0, cachedPngs = 0; long storageBytes = 0;
    void Check(RenderedPage page)
    {
        if (page.PageNumber == 1 && (page.Primitives.OfType<DrawTextPrimitive>().First().SemanticValue is not decimal value || value != 1 + session * 1000m))
        {
            throw new InvalidOperationException("Cross-session data contamination or scalar type loss.");
        }
    }
    if (mode == "stream")
    {
        await foreach (var page in paginator.StreamPagesAsync(request))
        {
            Check(page);
            if (actualPages++ == 0) firstPageMs = watch.Elapsed.TotalMilliseconds;
        }
    }
    else if (mode is "prepared" or "preview-window")
    {
        using var document = await paginator.PrepareAsync(request);
        actualPages = document.PageCount; storageBytes = document.StorageBytes;
        Check(document.GetPage(0));
        if (mode == "prepared") { firstPageMs = watch.Elapsed.TotalMilliseconds; }
        else
        {
            var cache = new PreviewPageCache(document);
            for (int i = 0; i < cache.Count; i++)
            {
                _ = cache[i];
                if (i == 0) firstPageMs = watch.Elapsed.TotalMilliseconds;
                if (cache.CachedPageCount > 3) throw new InvalidOperationException("Unbounded preview cache.");
            }
            cachedPngs = cache.CachedPageCount;
        }
    }
    else
    {
        var document = await paginator.PaginateAsync(request);
        actualPages = document.PageCount; Check(document.Pages[0]);
        if (mode == "buffered") { firstPageMs = watch.Elapsed.TotalMilliseconds; }
        else
        {
            using var renderer = new SkiaRenderingContext();
            RenderedReportPlayer.Play(document, renderer);
            var images = Enumerable.Range(0, renderer.Pages.Count).Select(index => renderer.GetPagePng(index)).ToList();
            firstPageMs = watch.Elapsed.TotalMilliseconds;
            cachedPngs = images.Count;
            GC.KeepAlive(images);
        }
    }
    if (actualPages != pages) throw new InvalidOperationException($"Expected {pages}, produced {actualPages} pages.");
    return new { actualPages, firstPageMs, storageBytes, cachedPngs };
}
