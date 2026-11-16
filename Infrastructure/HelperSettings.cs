using System.Text.Json;

namespace JMDataHelper.Infrastructure;

public sealed class HelperSettings
{
    public int Port { get; set; } = 8765;
    public string[] AllowedOrigins { get; set; } = ["http://viz-plt-mos-gwy:8177"];
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int CacheSeconds { get; set; } = 45;
    public string ListenUrl => $"http://127.0.0.1:{Port}";

    public static HelperSettings Load(string path)
    {
        var settings = File.Exists(path)
            ? JsonSerializer.Deserialize<HelperSettings>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("config.json je prazan.")
            : new HelperSettings();
        if (settings.Port is < 1024 or > 65535 || settings.RequestTimeoutSeconds is < 1 or > 120 || settings.CacheSeconds is < 0 or > 300)
            throw new InvalidDataException("Neispravan port, timeout ili cache u config.json.");
        if (settings.AllowedOrigins is null || settings.AllowedOrigins.Length == 0 || settings.AllowedOrigins.Any(x =>
            !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || x.EndsWith('/')))
            throw new InvalidDataException("AllowedOrigins mora sadržavati HTTP/HTTPS origin adrese bez završne kose crte (npr. http://viz-plt-mos-gwy:8177).");
        return settings;
    }
}
