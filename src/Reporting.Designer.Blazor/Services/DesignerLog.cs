using Microsoft.Extensions.Logging;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Structured log events of the Designer. They carry no report content: exception messages can quote an
/// expression, a field value or a file name (an <c>ExpressionEvaluationException</c> includes the whole expression),
/// and a logging provider persists the exception object as-is. So the exception itself is never forwarded — only its
/// type chain, which is enough to find the failing code path.</summary>
internal static partial class DesignerLog
{
    internal static void RepxLoadFailed(ILogger logger, Exception exception) => RepxLoadFailedCore(logger, Describe(exception));

    internal static void RdlImportFailed(ILogger logger, Exception exception) => RdlImportFailedCore(logger, Describe(exception));

    internal static void PreviewFailed(ILogger logger, Exception exception) => PreviewFailedCore(logger, Describe(exception));

    internal static void ExportFailed(ILogger logger, string format, Exception exception)
        => ExportFailedCore(logger, format, Describe(exception));

    internal static void PrintFailed(ILogger logger, Exception exception) => PrintFailedCore(logger, Describe(exception));

    internal static void MonacoMountFailed(ILogger logger, Exception exception)
        => MonacoMountFailedCore(logger, Describe(exception));

    // "System.IO.IOException <- System.UnauthorizedAccessException": outer to inner, type names only.
    private static string Describe(Exception exception)
    {
        var types = new List<string>();
        for (var current = exception; current is not null && types.Count < 8; current = current.InnerException)
        {
            types.Add(current.GetType().FullName ?? current.GetType().Name);
        }
        return string.Join(" <- ", types);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Falha ao abrir um arquivo .repx ({ExceptionType})")]
    private static partial void RepxLoadFailedCore(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Falha ao importar um arquivo .rdl ({ExceptionType})")]
    private static partial void RdlImportFailedCore(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Falha ao gerar o preview ({ExceptionType})")]
    private static partial void PreviewFailedCore(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Falha ao exportar no formato {Format} ({ExceptionType})")]
    private static partial void ExportFailedCore(ILogger logger, string format, string exceptionType);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Falha ao imprimir ({ExceptionType})")]
    private static partial void PrintFailedCore(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Falha ao montar o editor Monaco ({ExceptionType})")]
    private static partial void MonacoMountFailedCore(ILogger logger, string exceptionType);
}
