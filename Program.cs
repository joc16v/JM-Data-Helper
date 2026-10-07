using System.Net;
using JMDataHelper.Infrastructure;
using JMDataHelper.Services;
using JMDataHelper.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace JMDataHelper;

internal static class Program
{
    public const string Version = "0.2.0";
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var instance = new SingleInstance();
        if (!instance.IsFirst) return;
        try
        {
            var configIndex = Array.IndexOf(args, "--config");
            var configPath = configIndex >= 0 && configIndex + 1 < args.Length ? args[configIndex + 1] : Path.Combine(AppContext.BaseDirectory, "config.json");
            var settings = HelperSettings.Load(configPath);
            var fileLogs = new FileLogProvider();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders(); builder.Logging.AddProvider(fileLogs);
            builder.WebHost.UseUrls(settings.ListenUrl);
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton<SourceRegistry>();
            builder.Services.AddSingleton(_ =>
            {
                var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"JMDataHelper/{Version}");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hr-HR,hr;q=0.9,en;q=0.7");
                return client;
            });
            builder.Services.AddSingleton<DhmzService>();
            builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(settings.AllowedOrigins).AllowAnyHeader().AllowAnyMethod()));
            using var app = builder.Build();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("JMDataHelper");
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers["Access-Control-Request-Private-Network"] == "true"
                    && settings.AllowedOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal))
                    context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                context.Response.Headers.CacheControl = "no-store";
                await next();
            });
            app.UseCors();
            var sources = app.Services.GetRequiredService<SourceRegistry>();
            var dhmz = app.Services.GetRequiredService<DhmzService>();
            app.MapGet("/api/health", () => Results.Ok(new { ok = true, app = "JM Data Helper", version = Version, listenUrl = settings.ListenUrl, pilotEdgeOrigin = settings.AllowedOrigins.First(), time = DateTimeOffset.Now }));
            app.MapGet("/api/sources", () => Results.Ok(new { ok = true, sources = sources.Snapshot() }));
            app.MapGet("/api/dhmz/warnings", async (int? day, bool? refresh, HttpContext context) =>
            {
                var requestedDay = day ?? 0;
                if (requestedDay is < 0 or > 2) return Results.BadRequest(new { ok = false, error = "day mora biti 0, 1 ili 2." });
                try { return Results.Ok(await dhmz.GetWarningsAsync(requestedDay, refresh == true, context.RequestAborted)); }
                catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or OperationCanceledException or System.Text.RegularExpressions.RegexMatchTimeoutException)
                { return Results.Json(new { ok = false, error = ex.Message, day = requestedDay }, statusCode: 502); }
            });
            try { app.Start(); }
            catch (Exception ex)
            {
                logger.LogError(ex, "API start failed at {Url}", settings.ListenUrl);
                MessageBox.Show($"Helper nije mogao otvoriti {settings.ListenUrl}.\nMožda već radi stari helper ili drugi program koristi taj port.\n\n{ex.Message}", "JM Data Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            logger.LogInformation("JM Data Helper {Version} started at {Url}", Version, settings.ListenUrl);
            using var form = new MainForm(dhmz, sources, settings, fileLogs.DirectoryPath);
            instance.Listen(form.ActivateFromOtherInstance);
            Application.Run(form);
            logger.LogInformation("Stopping JM Data Helper");
            app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "JM Data Helper – pokretanje", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
