using JMDataHelper.Infrastructure;
using JMDataHelper.Models;
using JMDataHelper.Sources;
using JMDataHelper.Sources.Dhmz;
using Microsoft.Extensions.Logging;
namespace JMDataHelper.Services;

public sealed class DhmzService : IDisposable
{
    public const string SourceId = "dhmz-meteoalarm";
    private readonly HttpClient _client;
    private readonly HelperSettings _settings;
    private readonly SourceRegistry _status;
    private readonly ILogger<DhmzService> _logger;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim[] _gates = [new(1), new(1), new(1)];
    private readonly CacheEntry?[] _cache = new CacheEntry?[3];
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
    public DhmzService(HttpClient client, HelperSettings settings, SourceRegistry status, ILogger<DhmzService> logger, TimeProvider? clock = null)
    {
        _client = client; _settings = settings; _status = status; _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _status.Register(SourceId, "DHMZ · Meteoalarm");
    }
    public async Task<DhmzWarningsResponse> GetWarningsAsync(int day, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (day is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(day));
        var requestedAt = _clock.GetUtcNow();
        await _gates[day].WaitAsync(cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), Zagreb).DateTime);
            var cached = _cache[day];
            if (cached is not null && cached.LocalDate == today && _clock.GetUtcNow() - cached.StoredAt < TimeSpan.FromSeconds(_settings.CacheSeconds)
                && (!forceRefresh || cached.StoredAt >= requestedAt))
            {
                _status.Success(SourceId, cached.Response.FetchedAt, true);
                _logger.LogInformation("DHMZ day={Day}: cache, fetched={FetchedAt}", day, cached.Response.FetchedAt);
                return cached.Response;
            }
            _status.Started(SourceId);
            var url = $"https://meteo.hr/naslovnica-upozorenja-zupanije.php?tab=upozorenja&dan={day}";
            using var response = await _client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = DhmzParser.Parse(day, url, html, _clock.GetUtcNow());
            _cache[day] = new(_clock.GetUtcNow(), today, parsed);
            _status.Success(SourceId, parsed.FetchedAt, false);
            _logger.LogInformation("DHMZ day={Day}: OK, counties={Counties}, sea={Sea}", day, parsed.Counties.Count, parsed.Sea.Count);
            return parsed;
        }
        catch (OperationCanceledException ex)
        {
            var message = cancellationToken.IsCancellationRequested ? "Dohvat je otkazan." : "DHMZ nije odgovorio unutar zadanog vremena.";
            _status.Failed(SourceId, message); _logger.LogWarning(ex, "DHMZ day={Day}: {Message}", day, message);
            throw new OperationCanceledException(message, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _status.Failed(SourceId, ex.Message); _logger.LogError(ex, "DHMZ day={Day}: failed", day); throw;
        }
        finally { _gates[day].Release(); }
    }
    public void Dispose() { foreach (var gate in _gates) gate.Dispose(); }
    private sealed record CacheEntry(DateTimeOffset StoredAt, DateOnly LocalDate, DhmzWarningsResponse Response);
}
