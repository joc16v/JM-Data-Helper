using System.Security.Principal;

namespace JMDataHelper.Infrastructure;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    public bool IsFirst { get; }
    public SingleInstance(string instanceName = "JMDataHelper")
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _mutex = new Mutex(true, $"Local\\{instanceName}-{user}", out var created);
        IsFirst = created;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{instanceName}-activate-{user}");
        if (!IsFirst) _activate.Set();
    }
    public void Listen(Action activate)
    {
        _listener = Task.Run(() =>
        {
            while (!_stop.IsCancellationRequested)
                if (_activate.WaitOne(500) && !_stop.IsCancellationRequested) activate();
        });
    }
    public void Dispose()
    {
        _stop.Cancel();
        _listener?.GetAwaiter().GetResult();
        _activate.Dispose();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _stop.Dispose();
    }
}
