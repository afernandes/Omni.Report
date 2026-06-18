using Reporting.CodeFirst;
using Reporting.DataSources.FileSystem;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Generates an "audit report" of the files inside the sample's binary output folder.
/// Demonstrates the <see cref="FileSystemDataSource"/>:
/// <list type="bullet">
/// <item>Fixed 10-column schema (Id, ParentId, Name, FullName, Size, …) — exposed
/// up-front so the field tree is populated before any rows are read.</item>
/// <item>Recursive enumeration with file/directory patterns.</item>
/// <item>Filter to skip directory rows (the IsDirectory flag is a Field too).</item>
/// <item>Sort by size descending so the biggest files appear first.</item>
/// </list>
/// </summary>
/// <remarks>
/// In real reports, point <c>RootDirectory</c> at the actual folder you want to
/// audit (logs, backups, exports). The sample uses <see cref="AppContext.BaseDirectory"/>
/// so it always has something to enumerate without setup.
/// </remarks>
public static class Sample09_FileSystemLogs
{
    public static Report Build()
    {
        var fs = new FileSystemDataSource("Files", new FileSystemDataSourceOptions
        {
            RootDirectory = AppContext.BaseDirectory,
            // *.* gives us every file; restrict to *.dll, *.pdb, or *.json for a real
            // audit. The DirectoryPattern * keeps recursion exploring every subfolder.
            FilePattern = "*",
            DirectoryPattern = "*",
            Recursive = true,
            IncludeDirectories = false, // file-only listing keeps the report tabular
        });

        return ReportBuilder
            .Create("Auditoria de Arquivos (FileSystem)")
            .Page(p => p.A4().Landscape().Margins(15))
            .DataSource("Files", fs)
            // Skip noise — Microsoft-internal symbol files and the obj folder add huge
            // numbers of rows that aren't interesting in an audit summary.
            .DataSourceFilter("Fields.Extension != '.pdb' && Fields.Extension != '.cache'")
            // Heaviest files first — useful for "what's eating disk?" reports.
            .DataSourceSortBy("Fields.Size", Reporting.Data.SortDirection.Descending)
            .ReportHeader(h => h.Height(24)
                .Text("Auditoria de Arquivos · Fonte FileSystem")
                    .At(0, 0).Size(267, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Diretório: {Parameters.Root}")
                    .At(0, 14).Size(267, 6)
                    .Center()
                    .Color(Color.Gray)
                .Line().From(0, 22).To(267, 22).Thickness(0.5))
            .Parameters(p => p
                .Add<string>("Root", prompt: "Diretório raiz",
                    defaultValue: AppContext.BaseDirectory))
            .PageHeader(h => h.Height(8)
                .Label("Arquivo").At(0, 0).Size(140, 6).Bold()
                .Label("Extensão").At(142, 0).Size(20, 6).Bold()
                .Label("Tamanho (B)").At(164, 0).Size(30, 6).Bold().AlignRight()
                .Label("Modificado em").At(196, 0).Size(40, 6).Bold().AlignRight()
                .Label("Criado em").At(238, 0).Size(29, 6).Bold().AlignRight()
                .Line().From(0, 6).To(267, 6).Thickness(0.25))
            .Group("PorExtensao", "Fields.Extension", g => g
                .Header(h => h.Height(8)
                    .Text("Extensão: {Fields.Extension}")
                        .At(0, 1).Size(267, 6)
                        .Font("Arial", 10, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C"))))
            .Detail(d => d.Height(5)
                .Text("{Fields.Name}").At(0, 0).Size(140, 5).Font("Arial", 8, FontStyle.Regular)
                .Text("{Fields.Extension}").At(142, 0).Size(20, 5).Font("Arial", 8, FontStyle.Regular)
                .Text("{Fields.Size:N0}").At(164, 0).Size(30, 5).AlignRight().Font("Arial", 8, FontStyle.Regular)
                .Text("{Fields.LastWriteTime:dd/MM/yyyy HH:mm}").At(196, 0).Size(40, 5).AlignRight().Font("Arial", 8, FontStyle.Regular)
                .Text("{Fields.CreationTime:dd/MM/yyyy HH:mm}").At(238, 0).Size(29, 5).AlignRight().Font("Arial", 8, FontStyle.Regular))
            .DetailNoRows("Nenhum arquivo encontrado no diretório.")
            .ReportFooter(f => f.Height(12)
                .Line().From(0, 0).To(267, 0).Thickness(0.5)
                .Text("Arquivos: {Count(Fields.Name)} · Tamanho total: {Sum(Fields.Size):N0} bytes")
                    .At(0, 2).Size(267, 6).Bold().AlignRight())
            .PageFooter(f => f.Height(6)
                .Text("OmniReport · Página {Page.Number} de {Page.Total}")
                    .At(0, 0).Size(267, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
    }
}
