using Microsoft.Extensions.Logging;

namespace LIFT.Infrastructure;

// Keeps operational errors and admin changes outside wwwroot, in one file per day.
public sealed class DailyFileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly object gate = new();

    public ILogger CreateLogger(string categoryName) => new DailyFileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var date = DateTimeOffset.UtcNow;
            var path = Path.Combine(directory, $"lift-{date:yyyy-MM-dd}.log");
            var line = $"{date:O} [{level}] {category}: {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            lock (gate) File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (IOException) { /* A failed log write must not interrupt a user request. */ }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class DailyFileLogger(DailyFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning ||
            (level >= LogLevel.Information && category.StartsWith("LIFT.Services.Admin", StringComparison.Ordinal));

        public void Log<TState>(LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) provider.Write(category, level, formatter(state, exception), exception);
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
