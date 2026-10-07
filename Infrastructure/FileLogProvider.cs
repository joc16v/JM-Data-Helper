using Microsoft.Extensions.Logging;

namespace JMDataHelper.Infrastructure;

public sealed class FileLogProvider : ILoggerProvider
{
    private readonly object _gate = new();
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JMDataHelper", "logs");
    public FileLogProvider()
    {
        Directory.CreateDirectory(DirectoryPath);
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.log"))
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-14))
                try { File.Delete(file); } catch (IOException) { }
    }
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }
    private void Write(string text)
    {
        lock (_gate)
        {
            try
            {
                var path = Path.Combine(DirectoryPath, $"helper-{DateTime.Now:yyyy-MM-dd}.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                    File.Move(path, path.Replace(".log", "-previous.log"), true);
                File.AppendAllText(path, text + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private sealed class FileLogger(FileLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= (category.StartsWith("Microsoft.") ? LogLevel.Warning : LogLevel.Information);
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) owner.Write($"{DateTimeOffset.Now:O} [{level}] {category}: {formatter(state, exception)}{(exception is null ? "" : Environment.NewLine + exception)}");
        }
    }
}
