using System.Drawing;
using System.Windows.Forms;
using HearHere.Audio;

namespace HearHere.Tray;

/// <summary>
/// Manages the system tray (NotifyIcon) for the app.
/// Uses System.Windows.Forms.NotifyIcon since WPF has no built-in tray support.
/// </summary>
public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;

    public event Action? OpenSettingsRequested;
    public event Action? SwitchNextRequested;
    public event Action? SwitchPreviousRequested;
    public event Action? QuitRequested;

    public TrayManager()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open Settings…", null, (_, _) => OpenSettingsRequested?.Invoke());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Switch Output (Next)", null, (_, _) => SwitchNextRequested?.Invoke());
        _menu.Items.Add("Switch Output (Previous)", null, (_, _) => SwitchPreviousRequested?.Invoke());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "HearHere",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _icon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void UpdateTooltip(string deviceName)
    {
        // NotifyIcon.Text max is 127 chars
        string text = $"HearHere — {deviceName}";
        if (text.Length > 127) text = text[..127];
        _icon.Text = text;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon tipIcon = ToolTipIcon.Info)
    {
        _icon.ShowBalloonTip(3000, title, text, tipIcon);
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon programmatically (speaker-like)
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            // Simple speaker shape
            g.FillRectangle(Brushes.White, 3, 5, 4, 6);
            g.FillPolygon(Brushes.White, new[]
            {
                new Point(7, 5), new Point(12, 2), new Point(12, 14), new Point(7, 11)
            });
            // Sound waves
            g.DrawArc(new Pen(Color.White, 1.2f), 12, 4, 4, 8, -60, 120);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
