using Microsoft.Extensions.Logging;
using System.Text;

namespace JMDataHelper.Infrastructure;

public sealed class FileLogProvider : ILoggerProvider
{
    private readonly object _gate = new();
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int RetentionDays = 14;
    private readonly TimeProvider _clock;
    private DateOnly? _lastCleanupDay;
    public string DirectoryPath { get; }
    public FileLogProvider(string? directoryPath = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        DirectoryPath = directoryPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JMDataHelper", "logs");
        Directory.CreateDirectory(DirectoryPath);
        Cleanup(_clock.GetLocalNow().DateTime);
    }
    private void Cleanup(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (_lastCleanupDay == today) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(DirectoryPath, "helper-*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length < 17) continue;
                var datePart = name.Substring(7, 10);
                if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", out var date) && date < today.AddDays(1 - RetentionDays))
                    File.Delete(file);
            }
            _lastCleanupDay = today;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }
    private void Write(string text)
    {
        lock (_gate)
        {
            try
            {
                var now = _clock.GetLocalNow().DateTime;
                Cleanup(now);
                var path = Path.Combine(DirectoryPath, $"helper-{now:yyyy-MM-dd}.log");
                // Bound individual exceptions/messages as well as file size.
                if (text.Length > 8192) text = text[..8192] + "… [skraćeno]";
                var entry = text + Environment.NewLine;
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(entry) > MaxFileBytes)
                    File.Move(path, path.Replace(".log", "-previous.log"), true);
                File.AppendAllText(path, entry, new UTF8Encoding(false));
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
