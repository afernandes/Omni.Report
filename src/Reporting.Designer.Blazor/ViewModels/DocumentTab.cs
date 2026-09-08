using System.Collections.ObjectModel;
using System.Collections.Specialized;
namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Owns the report and all document-specific editing state.</summary>
public sealed class DocumentTab : Notifying, IDisposable
{
    public DocumentTab(string fileName, ReportDefinitionViewModel report)
    {
        _fileName = fileName;
        _report = report;
        _report.Changed += OnReportChanged;
        DataSources.CollectionChanged += OnCatalogChanged;
        Relations.CollectionChanged += OnCatalogChanged;
        Parameters.CollectionChanged += OnCatalogChanged;
        Variables.CollectionChanged += OnCatalogChanged;
        History.Changed += RaiseChanged;
    }
    private string _fileName;
    public string FileName { get => _fileName; set => Set(ref _fileName, value); }
    private ReportDefinitionViewModel _report;
    public ReportDefinitionViewModel Report
    {
        get => _report;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_report, value)) return;
            _report.Changed -= OnReportChanged;
            _report = value;
            _report.Changed += OnReportChanged;
            IsDirty = true;
            RaiseChanged();
        }
    }
    public CommandHistory History { get; } = new();
    public ObservableCollection<DesignerDataSource> DataSources { get; } = [];
    public ObservableCollection<DesignerRelation> Relations { get; } = [];
    public ObservableCollection<DesignerParameter> Parameters { get; } = [];
    public ObservableCollection<DesignerVariable> Variables { get; } = [];
    public Reporting.DataSources.DataSourceRegistry? PreviewDataRegistry { get; set; }
    public Dictionary<string, string?> ParameterValues { get; } = new(StringComparer.Ordinal);
    private bool _isDirty;
    public bool IsDirty { get => _isDirty; internal set => Set(ref _isDirty, value); }
    public string Icon => FileName.Contains("Cupom", StringComparison.OrdinalIgnoreCase) ? "receipt"
        : FileName.Contains("DRE", StringComparison.OrdinalIgnoreCase) ? "bar-chart-3" : "file-text";
    private readonly HashSet<Notifying> _catalogItems = new(ReferenceEqualityComparer.Instance);
    private void OnReportChanged() { IsDirty = true; RaiseChanged(); }
    private void OnCatalogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in _catalogItems) item.Changed -= OnReportChanged;
        _catalogItems.Clear();
        foreach (var item in DataSources.Cast<Notifying>().Concat(Relations).Concat(Parameters).Concat(Variables))
        {
            if (_catalogItems.Add(item)) item.Changed += OnReportChanged;
        }
        OnReportChanged();
    }
    public void Dispose()
    {
        _report.Changed -= OnReportChanged;
        History.Changed -= RaiseChanged;
        DataSources.CollectionChanged -= OnCatalogChanged;
        Relations.CollectionChanged -= OnCatalogChanged;
        Parameters.CollectionChanged -= OnCatalogChanged;
        Variables.CollectionChanged -= OnCatalogChanged;
        foreach (var item in _catalogItems) item.Changed -= OnReportChanged;
        _catalogItems.Clear();
        History.Clear();
        PreviewDataRegistry = null;
        ParameterValues.Clear();
    }
}
