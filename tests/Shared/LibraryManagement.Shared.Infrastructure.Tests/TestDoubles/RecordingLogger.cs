using Microsoft.Extensions.Logging;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// One line as a sink saw it: the level and the rendered message the assertions care about, and the
// exception when one was attached.
internal sealed record LogLine(LogLevel Level, string Message, Exception? Exception);

// Also an ILoggerFactory, because the logging behavior asks for one to name its category after the
// message rather than after itself; whatever category is asked for, the recording is the same.
internal sealed class RecordingLogger : ILogger, ILoggerFactory
{
    public List<LogLine> Lines { get; } = [];

    public string? Category { get; private set; }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Lines.Add(new LogLine(logLevel, formatter(state, exception), exception));
    }

    public ILogger CreateLogger(string categoryName)
    {
        Category = categoryName;
        return this;
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
