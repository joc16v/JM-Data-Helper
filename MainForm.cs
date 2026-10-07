using System.Diagnostics;
using JMDataHelper.Infrastructure;
using JMDataHelper.Services;
using JMDataHelper.Sources;
namespace JMDataHelper;

public sealed class MainForm : Form
{
    private readonly DhmzService _dhmz;
    private readonly SourceRegistry _sources;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly Label _sourceStatus;
    private readonly Label _lastSuccess;
    private readonly Label _error;
    private readonly Button _test;
    private readonly CancellationTokenSource _closing = new();
    private bool _exit;
    private bool _resourcesDisposed;
    public MainForm(DhmzService dhmz, SourceRegistry sources, HelperSettings settings, string logFolder)
    {
        _dhmz = dhmz; _sources = sources;
        Text = $"JM Data Helper {Program.Version}";
        ClientSize = new Size(620, 335); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        var title = new Label { Text = "JM Data Helper", AutoSize = true, Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Location = new(20, 18) };
        var api = new Label { Text = $"API radi · {settings.ListenUrl}", AutoSize = true, ForeColor = Color.DarkGreen, Location = new(22, 62) };
        _sourceStatus = new Label { Text = "DHMZ: još nema dohvaćanja", AutoSize = true, Location = new(22, 94) };
        _lastSuccess = new Label { Text = "Zadnji uspješan dohvat: —", AutoSize = true, Location = new(22, 120) };
        _error = new Label { AutoSize = false, Size = new(575, 55), Location = new(22, 145), ForeColor = Color.Firebrick, AutoEllipsis = true };
        _test = new Button { Text = "Test DHMZ (sutra)", Size = new(170, 35), Location = new(22, 208) };
        _test.Click += async (_, _) => await TestAsync();
        var logs = new Button { Text = "Otvori logove", Size = new(150, 35), Location = new(206, 208) };
        logs.Click += (_, _) => Open(logFolder);
        var health = new Button { Text = "Provjeri API", Size = new(150, 35), Location = new(370, 208) };
        health.Click += (_, _) => Open(settings.ListenUrl + "/api/health");
        var note = new Label { Text = "Zatvaranje prozora ostavlja helper pokrenut u trayu.\nZa prekid odaberi Izlaz na tray ikoni.", AutoSize = true, ForeColor = SystemColors.GrayText, Location = new(22, 266) };
        Controls.AddRange([title, api, _sourceStatus, _lastSuccess, _error, _test, logs, health, note]);
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Otvori JM Data Helper", null, (_, _) => Restore());
        _menu.Items.Add("Otvori logove", null, (_, _) => Open(logFolder));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Izlaz", null, (_, _) => { _exit = true; Close(); });
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "JM Data Helper · API radi", ContextMenuStrip = _menu, Visible = true };
        _tray.DoubleClick += (_, _) => Restore();
        _sources.Changed += SourceChanged;
        FormClosing += OnClosing;
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        Shown += (_, _) => UpdateStatus();
    }
    public void ActivateFromOtherInstance()
    {
        if (!IsDisposed && IsHandleCreated)
            try { BeginInvoke(Restore); } catch (InvalidOperationException) { }
    }
    private void Restore() { if (IsDisposed) return; Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void SourceChanged()
    {
        if (!IsDisposed && IsHandleCreated)
            try { BeginInvoke(UpdateStatus); } catch (InvalidOperationException) { }
    }
    private void UpdateStatus()
    {
        if (IsDisposed) return;
        var status = _sources.Snapshot().First(s => s.Id == DhmzService.SourceId);
        _sourceStatus.Text = status.State switch { "loading" => "DHMZ: dohvaćam…", "ok" => "DHMZ: OK" + (status.FromCache ? " · cache" : ""), "error" => "DHMZ: greška", _ => "DHMZ: još nema dohvaćanja" };
        _lastSuccess.Text = "Zadnji uspješan dohvat: " + (status.LastSuccess?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "—");
        _error.Text = status.LastError ?? "";
        _tray.Text = "JM Data Helper · " + (status.State == "error" ? "DHMZ greška" : "API radi");
        _test.Enabled = !_closing.IsCancellationRequested && status.State != "loading";
    }
    private async Task TestAsync()
    {
        _test.Enabled = false;
        try { await _dhmz.GetWarningsAsync(1, true, _closing.Token); }
        catch (Exception) { /* Source status and logs contain the failure. */ }
        finally { if (!IsDisposed && !_closing.IsCancellationRequested) UpdateStatus(); }
    }
    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_exit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        _closing.Cancel(); _sources.Changed -= SourceChanged; _tray.Visible = false;
    }
    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "JM Data Helper", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _closing.Cancel(); _sources.Changed -= SourceChanged; _tray.Dispose(); _menu.Dispose(); _closing.Dispose();
        }
        base.Dispose(disposing);
    }
}
