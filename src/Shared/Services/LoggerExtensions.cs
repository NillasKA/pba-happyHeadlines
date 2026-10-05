using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace HappyHeadlines.Shared;

public static class LoggerExtensions
{
    public static IDisposable Here(
        this ILogger logger,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var properties = new Dictionary<string, object?>
        {
            ["MemberName"] = memberName,
            ["FilePath"] = filePath,
            ["LineNumber"] = lineNumber
        };
        return logger.BeginScope(properties) ?? NoopDisposable.Instance;
    }

    public static IDisposable BeginTimedOperation(
        this ILogger logger,
        string operation,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        new TimedOperation(logger, operation, memberName, filePath, lineNumber);
}
