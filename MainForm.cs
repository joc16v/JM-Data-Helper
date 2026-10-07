using System.Diagnostics;
using JMDataHelper.Services;

namespace JMDataHelper;

public sealed class MainForm : Form
{
    private readonly DhmzService _dhmz;
    private readonly Label _apiStatus;
    private readonly Label _dhmzStatus;
    private readonly Label _lastRequest;
    private readonly Button _testDhmzButton;

    public MainForm(DhmzService dhmz)
    {
        _dhmz = dhmz;

        Text = "JM Data Helper 0.1";
        ClientSize = new Size(520, 260);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var title = new Label
        {
            Text = "JM Data Helper",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18)
        };

        _apiStatus = new Label
        {
            Text = $"API: RUNNING   {Program.ListenUrl}",
            AutoSize = true,
            Location = new Point(22, 62)
        };

        _dhmzStatus = new Label
        {
            Text = "DHMZ: nije testiran",
            AutoSize = true,
            Location = new Point(22, 88)
        };

        _lastRequest = new Label
        {
            Text = "Zadnji dohvat: -",
            AutoSize = true,
            Location = new Point(22, 114)
        };

        _testDhmzButton = new Button
        {
            Text = "Test DHMZ (sutra)",
            Size = new Size(155, 34),
            Location = new Point(22, 157)
        };
        _testDhmzButton.Click += TestDhmzButton_Click;

        var openHealthButton = new Button
        {
            Text = "Otvori /api/health",
            Size = new Size(155, 34),
            Location = new Point(190, 157)
        };
        openHealthButton.Click += (_, _) => OpenUrl($"{Program.ListenUrl}/api/health");

        var copyButton = new Button
        {
            Text = "Kopiraj API URL",
            Size = new Size(135, 34),
            Location = new Point(357, 157)
        };
        copyButton.Click += (_, _) => Clipboard.SetText($"{Program.ListenUrl}/api/dhmz/warnings?day=1");

        var note = new Label
        {
            Text = $"Pilot Edge origin: {Program.PilotEdgeOrigin}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(22, 218)
        };

        Controls.AddRange(new Control[]
        {
            title, _apiStatus, _dhmzStatus, _lastRequest,
            _testDhmzButton, openHealthButton, copyButton, note
        });
    }

    private async void TestDhmzButton_Click(object? sender, EventArgs e)
    {
        _testDhmzButton.Enabled = false;
        _dhmzStatus.Text = "DHMZ: dohvaćam...";

        try
        {
            var data = await _dhmz.GetWarningsAsync(day: 1, forceRefresh: true);
            var warningCount = data.Counties.Count(x => x.Level > 1);
            _dhmzStatus.Text = $"DHMZ: OK   ({data.Counties.Count} županija, {warningCount} s upozorenjem)";
            _lastRequest.Text = $"Zadnji dohvat: {data.FetchedAt:dd.MM.yyyy HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _dhmzStatus.Text = "DHMZ: ERROR";
            _lastRequest.Text = $"Zadnji pokušaj: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
            MessageBox.Show(ex.Message, "DHMZ test", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _testDhmzButton.Enabled = true;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "JM Data Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
