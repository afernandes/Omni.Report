using Microsoft.Extensions.Logging;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Structured log events of the Designer. The message templates carry no report content — names, field values
/// and expressions can be personal or business data — only the operation and, for exports, the format slug.</summary>
internal static partial class DesignerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Falha ao abrir um arquivo .repx")]
    internal static partial void RepxLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Falha ao importar um arquivo .rdl")]
    internal static partial void RdlImportFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Falha ao gerar o preview")]
    internal static partial void PreviewFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Falha ao exportar no formato {Format}")]
    internal static partial void ExportFailed(ILogger logger, string format, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Falha ao imprimir")]
    internal static partial void PrintFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Falha ao montar o editor Monaco")]
    internal static partial void MonacoMountFailed(ILogger logger, Exception exception);
}
