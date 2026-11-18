using Microsoft.Extensions.Logging;

namespace EmsAtHome.EebusCli;

/// <summary>
/// Minimal append-only file logger, so the console can stay quiet enough to type interactive
/// commands (see appsettings.json - "EEBUS.Ship"/"EEBUS.Spine" default to Trace/Debug here but only
/// Warning on the console) while the full SHIP/SPINE trace is still captured for later review.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly Lock _lock = new();

    public FileLoggerProvider(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _path, _lock);

    public void Dispose() { }

    private sealed class FileLogger(string category, string path, Lock @lock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {logLevel,-5} {category}: {formatter(state, exception)}";
            if (exception != null) line += Environment.NewLine + exception;

            lock (@lock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
    }
}
