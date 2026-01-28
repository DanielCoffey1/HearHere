using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using HearHere.Audio;
using HearHere.Config;
using HearHere.Hotkeys;
using HearHere.Logging;
using HearHere.OSD;
using HearHere.Tray;

namespace HearHere;

public partial class App : Application
{
    private AudioDeviceService _audioService = null!;
    private DeviceSwitcher _switcher = null!;
    private AppConfig _config = null!;
    private TrayManager _tray = null!;
    private GlobalHotkeyManager _hotkeyManager = null!;
    private MainWindow _hiddenWindow = null!;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Write("HearHere starting.");

        try
        {
            _config = AppConfig.Load();
            _audioService = new AudioDeviceService();
            _switcher = new DeviceSwitcher(_audioService, _config);

            // Hidden window for HWND
            _hiddenWindow = new MainWindow();
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            // Tray
            _tray = new TrayManager();
            _tray.OpenSettingsRequested += ShowSettings;
            _tray.SwitchNextRequested += () => DoSwitch(forward: true);
            _tray.SwitchPreviousRequested += () => DoSwitch(forward: false);
            _tray.QuitRequested += () =>
            {
                Log.Write("Quit requested.");
                Shutdown();
            };

            UpdateTooltip();

            // Hotkeys
            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.Initialize(_hiddenWindow);
            RegisterHotkeys();

            // Listen for device changes
            _audioService.DevicesChanged += () => Dispatcher.BeginInvoke(UpdateTooltip);
            _audioService.DefaultDeviceChanged += _ => Dispatcher.BeginInvoke(UpdateTooltip);

            Log.Write("HearHere started successfully.");
        }
        catch (Exception ex)
        {
            Log.Write($"Fatal startup error: {ex}");
            MessageBox.Show($"HearHere failed to start:\n{ex.Message}", "HearHere", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void DoSwitch(bool forward)
    {
        var device = _switcher.Switch(forward);
        if (device != null)
        {
            OsdWindow.ShowToast(device.FriendlyName);
            UpdateTooltip();
        }
        else
        {
            _tray.ShowBalloon("HearHere", "No enabled devices to switch to.", System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private void UpdateTooltip()
    {
        var devices = _audioService.GetPlaybackDevices();
        var def = devices.FirstOrDefault(d => d.IsDefault);
        _tray.UpdateTooltip(def?.FriendlyName ?? "No device");
    }

    private void RegisterHotkeys()
    {
        _hotkeyManager.UnregisterAll();
        var errors = new List<string>();

        if (!_config.NextDeviceHotkey.IsEmpty)
        {
            int id = _hotkeyManager.Register(_config.NextDeviceHotkey.Modifiers, _config.NextDeviceHotkey.Key,
                () => DoSwitch(forward: true));
            if (id < 0) errors.Add($"Next: {_config.NextDeviceHotkey.DisplayString}");
        }

        if (!_config.PreviousDeviceHotkey.IsEmpty)
        {
            int id = _hotkeyManager.Register(_config.PreviousDeviceHotkey.Modifiers, _config.PreviousDeviceHotkey.Key,
                () => DoSwitch(forward: false));
            if (id < 0) errors.Add($"Previous: {_config.PreviousDeviceHotkey.DisplayString}");
        }

        if (errors.Count > 0)
        {
            string msg = "Failed to register hotkeys (may conflict with another app):\n" + string.Join("\n", errors);
            _tray.ShowBalloon("HearHere — Hotkey Error", msg, System.Windows.Forms.ToolTipIcon.Warning);
            Log.Write(msg);
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_audioService, _config, () =>
        {
            RegisterHotkeys();
            UpdateTooltip();
        });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _tray?.Dispose();
        _audioService?.Dispose();
        Log.Write("HearHere exited.");
        base.OnExit(e);
    }
}
