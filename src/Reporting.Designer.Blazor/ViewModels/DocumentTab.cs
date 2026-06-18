namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>A single open report in the designer. The active tab's <see cref="Report"/> is
/// what the canvas and property grid bind to.</summary>
public sealed class DocumentTab : Notifying
{
    public DocumentTab(string fileName, ReportDefinitionViewModel report)
    {
        _fileName = fileName;
        _report = report;
        _report.Changed += () => IsDirty = true;
    }

    private string _fileName;
    public string FileName { get => _fileName; set => Set(ref _fileName, value); }

    private ReportDefinitionViewModel _report;
    public ReportDefinitionViewModel Report
    {
        get => _report;
        set => Set(ref _report, value);
    }

    private bool _isDirty;
    public bool IsDirty { get => _isDirty; internal set => Set(ref _isDirty, value); }

    /// <summary>The icon hint shown in the tab strip (matches mock: file-text / receipt / chart).</summary>
    public string Icon => FileName.Contains("Cupom", StringComparison.OrdinalIgnoreCase) ? "receipt"
                        : FileName.Contains("DRE",   StringComparison.OrdinalIgnoreCase) ? "bar-chart-3"
                        : "file-text";
}
