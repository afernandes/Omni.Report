using System.Collections.ObjectModel;
using Reporting.Geometry;
using Reporting.Serialization;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Root state of the designer: open documents, the active tab, command history, selection
/// and a few session-level flags (snap, zoom, theme, preview).
/// </summary>
public sealed class DesignerState : Notifying
{
    private static readonly RepxSerializer _serializer = new();

    public DesignerState()
        : this(new ReportDefinitionViewModel("Untitled")) { }

    public DesignerState(ReportDefinitionViewModel report)
    {
        var initial = new DocumentTab(report.Name + ".repx", report);
        Tabs.Add(initial);
        _activeTab = initial;
        SubscribeTab(initial);

        // Seed a single sample data source — hosts can replace via Replace*Catalog().
        DataSources = new ObservableCollection<DesignerDataSource> { DesignerDataSource.SampleVendas };
        Parameters = new ObservableCollection<DesignerParameter>
        {
            new("DataInicio", DesignerFieldType.Date, "01/10/2025"),
            new("DataFim",    DesignerFieldType.Date, "31/10/2025"),
        };
        Relations = new ObservableCollection<DesignerRelation>();
        Relations.CollectionChanged += (_, _) => RaiseChanged();
    }

    public ObservableCollection<DocumentTab> Tabs { get; } = [];

    private DocumentTab _activeTab = default!;
    public DocumentTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value)) return;
            _activeTab = value;
            SelectedElement = null;
            RaiseChanged();
        }
    }

    /// <summary>The active document's report. Convenience proxy.</summary>
    public ReportDefinitionViewModel Report => _activeTab.Report;

    public CommandHistory History { get; } = new();

    public ObservableCollection<DesignerDataSource> DataSources { get; }

    /// <summary>Master→detail relationships between data sources. Drives the Relations panel
    /// in the data tree and is consumed by the runtime to filter child rows for each parent.</summary>
    public ObservableCollection<DesignerRelation> Relations { get; }
    public ObservableCollection<DesignerParameter>  Parameters  { get; }

    public IEnumerable<DesignerField> ActiveFields
        => DataSources.SelectMany(ds => ds.Fields);

    private ElementViewModel? _selectedElement;
    public ElementViewModel? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (ReferenceEquals(_selectedElement, value)) return;
            // Clear all multi-selection when single-set is used.
            foreach (var s in _selectedElements) s.IsSelected = false;
            _selectedElements.Clear();
            _selectedElement = value;
            if (_selectedElement is not null)
            {
                _selectedElement.IsSelected = true;
                _selectedElements.Add(_selectedElement);
                // Auto-promote the element's band as the active band. Click-to-add and
                // paste both honour ActiveBand, so this keeps insertion consistent with
                // the user's current focus without an extra click on the band strip.
                var owningBand = Report.Bands.FirstOrDefault(b => b.Elements.Contains(_selectedElement));
                if (owningBand is not null) _activeBand = owningBand;
            }
            RaiseChanged();
        }
    }

    private readonly ObservableCollection<ElementViewModel> _selectedElements = new();
    /// <summary>Live read-only view of the multi-selection. <see cref="SelectedElement"/> is
    /// always the FIRST entry (last one toggled); operations that target many elements (align,
    /// distribute, change property) iterate this set.</summary>
    public IReadOnlyList<ElementViewModel> SelectedElements => _selectedElements;

    public void ToggleSelection(ElementViewModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_selectedElements.Contains(element))
        {
            _selectedElements.Remove(element);
            element.IsSelected = false;
            // Re-anchor SelectedElement to the next remaining element (if any).
            _selectedElement = _selectedElements.Count > 0 ? _selectedElements[^1] : null;
        }
        else
        {
            _selectedElements.Add(element);
            element.IsSelected = true;
            _selectedElement = element;
        }
        RaiseChanged();
    }

    public void SelectMany(IEnumerable<ElementViewModel> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        foreach (var s in _selectedElements) s.IsSelected = false;
        _selectedElements.Clear();
        foreach (var e in elements)
        {
            _selectedElements.Add(e);
            e.IsSelected = true;
        }
        _selectedElement = _selectedElements.Count > 0 ? _selectedElements[^1] : null;
        RaiseChanged();
    }

    public void ClearSelection()
    {
        foreach (var s in _selectedElements) s.IsSelected = false;
        _selectedElements.Clear();
        _selectedElement = null;
        RaiseChanged();
    }

    private bool _snapToGrid = true;
    public bool SnapToGrid { get => _snapToGrid; set => Set(ref _snapToGrid, value); }

    private Unit _gridStep = Unit.FromMm(1);
    public Unit GridStep { get => _gridStep; set => Set(ref _gridStep, value); }

    private double _zoom = 1.0;
    public double Zoom { get => _zoom; set => Set(ref _zoom, Math.Clamp(value, 0.25, 4.0)); }

    private string _theme = "light";
    /// <summary>"light" or "dark" — drives <c>data-theme</c> on the root.</summary>
    public string Theme { get => _theme; set => Set(ref _theme, value == "dark" ? "dark" : "light"); }

    private bool _isPreviewing;
    public bool IsPreviewing { get => _isPreviewing; set => Set(ref _isPreviewing, value); }

    private bool _commandPaletteOpen;
    public bool CommandPaletteOpen { get => _commandPaletteOpen; set => Set(ref _commandPaletteOpen, value); }

    private bool _gridVisible = true;
    public bool GridVisible { get => _gridVisible; set => Set(ref _gridVisible, value); }

    private BandViewModel? _activeBand;
    /// <summary>The "focused" band — receives paste, click-toolbox-add, and Insert menu
    /// commands. Auto-updates to the band of the selected element. Null = no band focus
    /// (falls back to Detail). Set by clicking on a band strip or empty band area.</summary>
    public BandViewModel? ActiveBand
    {
        get => _activeBand;
        set => Set(ref _activeBand, value);
    }

    /// <summary>Internal clipboard for cut/copy/paste — single-element scope for now.</summary>
    public ElementViewModel? Clipboard { get; set; }

    /// <summary>Optional in-memory data the host supplies so the Designer's <b>preview</b> can
    /// render data-bound elements (charts, map, tablix, …) without a live database — e.g. the
    /// sandbox loading a code-first sample's own <c>Report.DataSources</c>. The preview pipeline
    /// merges it into the runtime registry; DB-materialised sources of the same name take
    /// precedence. Cleared automatically whenever the active report is replaced.</summary>
    public Reporting.DataSources.DataSourceRegistry? PreviewDataRegistry { get; set; }

    public bool IsDirty => ActiveTab.IsDirty;

    public void ToggleTheme() => Theme = Theme == "dark" ? "light" : "dark";

    /// <summary>Replaces the active tab's report (e.g. on Load / New).</summary>
    public void ReplaceActiveReport(ReportDefinitionViewModel newReport)
    {
        ArgumentNullException.ThrowIfNull(newReport);
        UnsubscribeTab(_activeTab);
        _activeTab.Report = newReport;
        SubscribeTab(_activeTab);
        SelectedElement = null;
        History.Clear();
        _activeTab.IsDirty = false;
        PreviewDataRegistry = null; // stale preview data no longer matches the new report
        RaiseChanged();
    }

    /// <summary>Adds a new empty tab and makes it active.</summary>
    public DocumentTab OpenNewDocument(string name = "Untitled")
    {
        var report = new ReportDefinitionViewModel(name);
        var tab = new DocumentTab(name + ".repx", report);
        Tabs.Add(tab);
        SubscribeTab(tab);
        ActiveTab = tab;
        return tab;
    }

    /// <summary>Closes a tab. The active tab can't be closed if it's the last one.</summary>
    public void CloseTab(DocumentTab tab)
    {
        if (Tabs.Count <= 1) return;
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        UnsubscribeTab(tab);
        Tabs.RemoveAt(idx);
        if (ReferenceEquals(tab, _activeTab))
        {
            ActiveTab = Tabs[Math.Max(0, idx - 1)];
        }
        else
        {
            RaiseChanged();
        }
    }

    /// <summary>Loads a .repx blob and replaces the active tab's report — including the
    /// embedded data sources and master-detail relations (when present).</summary>
    public void Load(byte[] repxBytes)
    {
        ArgumentNullException.ThrowIfNull(repxBytes);
        var definition = _serializer.LoadFromBytes(repxBytes);
        ReplaceActiveReport(ReportDefinitionViewModel.FromDefinition(definition));

        // Restore designer-side catalogs from the loaded definition. We replace wholesale —
        // .repx is the source of truth for the just-opened report.
        DataSources.Clear();
        if (definition.DataSources.Count > 0)
        {
            foreach (var ds in definition.DataSources)
            {
                DataSources.Add(DesignerDataSource.FromDefinition(ds));
            }
        }
        else
        {
            // Backwards-compat: legacy .repx files have no data sources persisted. Seed the
            // sample so the canvas still binds something.
            DataSources.Add(DesignerDataSource.SampleVendas);
        }

        Relations.Clear();
        foreach (var r in ReportDefinitionViewModel.ExtractRelations(definition))
        {
            Relations.Add(r);
        }
    }

    /// <summary>Serializes the active tab to a .repx byte array — embeds the current data
    /// sources and master-detail relations so reopening restores the full configuration.</summary>
    public byte[] Save()
    {
        var bytes = _serializer.SaveToBytes(BuildDefinition());
        _activeTab.IsDirty = false;
        RaiseChanged();
        return bytes;
    }

    /// <summary>Builds the immutable <see cref="ReportDefinition"/> with the designer's
    /// current data sources and master-detail relations attached.</summary>
    public ReportDefinition BuildDefinition() => Report.Build(DataSources, Relations);

    private void SubscribeTab(DocumentTab tab)
    {
        tab.Changed += OnTabChanged;
    }

    private void UnsubscribeTab(DocumentTab tab)
    {
        tab.Changed -= OnTabChanged;
    }

    private void OnTabChanged() => RaiseChanged();
}
