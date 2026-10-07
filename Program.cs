using JMDataHelper.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JMDataHelper;

internal static class Program
{
    public const string PilotEdgeOrigin = "http://viz-plt-mos-gwy:8177";
    public const string ListenUrl = "http://127.0.0.1:8765";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(ListenUrl);

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(PilotEdgeOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.Services.AddSingleton<DhmzService>();

        var app = builder.Build();

        // Chromium can preflight requests from a LAN-hosted page to localhost as
        // a Private Network Access request. This header makes the helper ready
        // for that preflight as well as ordinary CORS.
        app.Use(async (context, next) =>
        {
            if (string.Equals(
                    context.Request.Headers["Access-Control-Request-Private-Network"].ToString(),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }

            await next();
        });

        app.UseCors();

        app.MapGet("/api/health", () => Results.Ok(new
        {
            ok = true,
            app = "JM Data Helper",
            version = "0.1.0",
            listenUrl = ListenUrl,
            pilotEdgeOrigin = PilotEdgeOrigin,
            time = DateTimeOffset.Now
        }));

        app.MapGet("/api/dhmz/warnings", async (int? day, bool? refresh, DhmzService dhmz) =>
        {
            var requestedDay = day ?? 0;
            if (requestedDay is < 0 or > 2)
            {
                return Results.BadRequest(new
                {
                    ok = false,
                    error = "Parameter 'day' must be 0 (today), 1 (tomorrow), or 2 (day after tomorrow)."
                });
            }

            try
            {
                var data = await dhmz.GetWarningsAsync(requestedDay, refresh == true);
                return Results.Ok(data);
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    ok = false,
                    error = ex.Message,
                    day = requestedDay
                }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        try
        {
            app.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"JM Data Helper nije mogao pokrenuti lokalni API na {ListenUrl}.\n\n{ex.Message}",
                "JM Data Helper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var dhmzService = app.Services.GetRequiredService<DhmzService>();
        Application.Run(new MainForm(dhmzService));

        app.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
