using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Composition.Tests.Logging;

/// <summary>
/// One line as a sink saw it, category included — the category carries the message's type name,
/// which is half of what the tests assert.
/// </summary>
public sealed record RecordedLine(string Category, LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// A logger provider that keeps every line, so a test can check the pipeline and the drain say
/// what they promise to say — and never what they promise to keep out.
/// </summary>
public sealed class RecordedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLine> _lines = new();

    public IReadOnlyCollection<RecordedLine> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Recorder(string category, ConcurrentQueue<RecordedLine> lines) : ILogger
    {
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
            => lines.Enqueue(new RecordedLine(category, logLevel, formatter(state, exception), exception));
    }
}
