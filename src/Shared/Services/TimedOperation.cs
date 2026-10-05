using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HappyHeadlines.Shared;

internal sealed class TimedOperation : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly IDisposable _scope;
    private bool _disposed;

    public TimedOperation(ILogger logger, string operation, string memberName, string filePath, int lineNumber)
    {
        _logger = logger;
        _operation = operation;
        _scope = logger.Here(memberName, filePath, lineNumber);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stopwatch.Stop();
        _logger.LogInformation("Timed operation {Operation} completed in {DurationMs} ms",
            _operation, _stopwatch.Elapsed.TotalMilliseconds);
        _scope.Dispose();
    }
}
