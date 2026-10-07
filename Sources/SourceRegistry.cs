namespace JMDataHelper.Sources;

public sealed record SourceStatus(string Id, string Name, string State, DateTimeOffset? LastAttempt, DateTimeOffset? LastSuccess, string? LastError, bool FromCache);

public sealed class SourceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SourceStatus> _sources = new();
    public event Action? Changed;
    public void Register(string id, string name)
    {
        lock (_gate) _sources.TryAdd(id, new(id, name, "idle", null, null, null, false));
    }
    public SourceStatus[] Snapshot()
    {
        lock (_gate) return _sources.Values.ToArray();
    }
    public void Started(string id) => Update(id, current => current with { State = "loading", LastAttempt = DateTimeOffset.Now, LastError = null, FromCache = false });
    public void Success(string id, DateTimeOffset fetchedAt, bool cached) => Update(id, current => current with { State = "ok", LastAttempt = DateTimeOffset.Now, LastSuccess = fetchedAt, LastError = null, FromCache = cached });
    public void Failed(string id, string error) => Update(id, current => current with { State = "error", LastError = error, FromCache = false });
    private void Update(string id, Func<SourceStatus, SourceStatus> update)
    {
        lock (_gate) _sources[id] = update(_sources[id]);
        // Status subscribers cannot interfere with the data provider.
        foreach (var handler in Changed?.GetInvocationList() ?? [])
            try { ((Action)handler)(); } catch (InvalidOperationException) { }
    }
}
