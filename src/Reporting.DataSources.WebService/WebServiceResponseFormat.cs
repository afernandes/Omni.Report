namespace Reporting.DataSources.WebService;

/// <summary>Response-format selector for <see cref="WebServiceDataSourceOptions.Format"/>.</summary>
public enum WebServiceResponseFormat
{
    /// <summary>Sniff <c>Content-Type</c> header.</summary>
    Auto,
    Json,
    Xml,
}
