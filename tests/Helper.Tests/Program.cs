using System.Net;
using JMDataHelper.Infrastructure;
using JMDataHelper.Services;
using JMDataHelper.Sources;
using JMDataHelper.Sources.Dhmz;
using Microsoft.Extensions.Logging.Abstractions;

var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dhmz-pointers.html"));
var parsed = DhmzParser.Parse(1, "https://meteo.hr/", html);
Check(parsed.Ok && parsed.Day == 1 && parsed.Counties.Count == 21 && parsed.Sea.Count == 6, "all 27 areas");
Check(parsed.Counties.Select(x => x.Id).Distinct().Count() == 21, "unique county IDs");
Check(parsed.Counties.Single(x => x.Id == "grad_zagreb").Level == 1, "Zagreb green=1");
Check(parsed.Counties.Single(x => x.Id == "primorsko-goranska_zupanija").Level == 3, "PG orange=3");
Check(parsed.Counties.Single(x => x.Id == "karlovacka_zupanija").Level == 2, "Karlovac yellow=2");
Check(parsed.Counties.All(x => x.Level is >=1 and <=4), "only valid levels");
var red = html.Replace("fd-c-map__regija--green", "fd-c-map__regija--red");
Check(DhmzParser.Parse(0, "source", red).Counties.Single(x => x.Id == "grad_zagreb").Level == 4, "red=4");
Throws(() => DhmzParser.Parse(0, "source", html.Replace("grad_zagreb_pointer", "missing_pointer")), "missing county rejects entire result");
Throws(() => DhmzParser.Parse(0, "source", html.Replace("zapadna_obala_istre_pointer", "missing_sea_pointer")), "missing sea rejects entire result");
Throws(() => DhmzParser.Parse(0, "source", "<html>server unavailable</html>"), "invalid HTML cannot become green");
Throws(() => DhmzParser.Parse(0, "source", html.Replace("fd-c-map__regija--green", "fd-c-map__regija--grey")), "unknown color cannot become green");
Check(DhmzParser.Parse(2, "source", html.Replace(" id=", "\n id=")).Counties.Count == 21, "attribute whitespace");

var clock = new Clock(DateTimeOffset.Parse("2026-10-07T21:59:50Z"));
var handler = new Handler(html);
using var client = new HttpClient(handler);
var sources = new SourceRegistry();
using var service = new DhmzService(client, new HelperSettings(), sources, NullLogger<DhmzService>.Instance, clock);
await service.GetWarningsAsync(0);
await service.GetWarningsAsync(0);
Check(handler.Calls == 1 && sources.Snapshot()[0].FromCache, "cache and shared source status");
clock.Now = clock.Now.AddSeconds(20); // Zagreb crossed midnight while cache is still <45 seconds old.
await service.GetWarningsAsync(0);
Check(handler.Calls == 2, "relative day cache invalidated at Zagreb midnight");
clock.Now = clock.Now.AddSeconds(1);
await service.GetWarningsAsync(0, true);
Check(handler.Calls == 3 && !sources.Snapshot()[0].FromCache, "explicit refresh bypasses old cache");
handler.Delay = 70;
clock.Now = clock.Now.AddSeconds(1);
await Task.WhenAll(service.GetWarningsAsync(1, true), service.GetWarningsAsync(1, true));
Check(handler.Calls == 4, "overlapping refreshes share one upstream request");
handler.Fail = true;
clock.Now = clock.Now.AddSeconds(1);
try { await service.GetWarningsAsync(2, true); throw new Exception("Expected failure"); } catch (HttpRequestException) { }
Check(sources.Snapshot()[0].State == "error" && sources.Snapshot()[0].LastSuccess is not null, "failure status retains last success");
handler.Fail = false;
using var cancel = new CancellationTokenSource(5);
try { await service.GetWarningsAsync(2, true, cancel.Token); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
Check(sources.Snapshot()[0].State == "error", "cancellation status");
await service.GetWarningsAsync(2, true);
Check(sources.Snapshot()[0].State == "ok", "recovery after failure/cancellation");
try { await service.GetWarningsAsync(3); throw new Exception("Expected bad day"); } catch (ArgumentOutOfRangeException) { }
Exception? uiFailure = null;
var uiThread = new Thread(() =>
{
    try
    {
        using var first = new SingleInstance();
        using var activated = new AutoResetEvent(false);
        first.Listen(() => activated.Set());
        using (var second = new SingleInstance())
            Check(first.IsFirst && !second.IsFirst && activated.WaitOne(2000), "single instance activates existing process");
        using var form = new JMDataHelper.MainForm(service, sources, new HelperSettings(), Path.GetTempPath());
        var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
        var onClosing = typeof(Form).GetMethod("OnFormClosing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        onClosing.Invoke(form, [closing]);
        Check(closing.Cancel, "X keeps helper running in tray");
        var shutdown = new FormClosingEventArgs(CloseReason.WindowsShutDown, false);
        onClosing.Invoke(form, [shutdown]);
        Check(!shutdown.Cancel, "Windows shutdown is not blocked");
        form.Dispose();
        form.Dispose();
        Check(true, "form and tray disposal is safe when repeated");
    }
    catch (Exception ex) { uiFailure = ex; }
});
uiThread.SetApartmentState(ApartmentState.STA);
uiThread.Start(); uiThread.Join();
if (uiFailure is not null) throw uiFailure;
Console.WriteLine("PASS: parser, levels, incomplete/unknown data rejection, cache, midnight, refresh coalescing, source status, cancellation and recovery.");

static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("OK: " + name); }
static void Throws(Action action, string name) { try { action(); } catch (InvalidDataException) { Check(true, name); return; } throw new Exception("FAILED: " + name); }
sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now; }
sealed class Handler(string html) : HttpMessageHandler
{
    public int Calls; public int Delay; public bool Fail;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        if (Delay > 0) await Task.Delay(Delay, cancellationToken);
        if (Fail) throw new HttpRequestException("Test network failure");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) };
    }
}
