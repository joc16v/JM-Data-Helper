namespace JMDataHelper.Infrastructure;

public static class AppIcon
{
    public static Icon Value { get; } = Load();
    private static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("JMDataHelper.AppIcon.ico")
            ?? throw new InvalidOperationException("Nedostaje ikona aplikacije.");
        using var loaded = new Icon(stream, new Size(32, 32));
        return (Icon)loaded.Clone();
    }
}
