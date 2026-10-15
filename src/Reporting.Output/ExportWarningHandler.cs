namespace Reporting.Output;

/// <summary>
/// Receives <see cref="ExportWarning"/>s raised during an export.
/// </summary>
/// <remarks>
/// Deliberately a CALLBACK on the exporter's options rather than a mutable <c>Warnings</c> property on the
/// exporter, the way <c>RdlExporter</c> does it. Exporters are registered as singletons by
/// <c>AddReporting</c>, so a mutable per-instance list would be shared by concurrent requests — exactly the
/// cross-request corruption that had to be fixed in <c>ReportPaginator</c>. A callback holds no state on the
/// exporter, so it stays safe to share.
/// </remarks>
public delegate void ExportWarningHandler(ExportWarning warning);
