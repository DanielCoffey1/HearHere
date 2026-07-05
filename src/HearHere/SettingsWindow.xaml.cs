using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using HearHere.Audio;
using HearHere.Config;
using HearHere.Tray;

namespace HearHere;

public partial class SettingsWindow : Window
{
    private readonly AudioDeviceService _audioService;
    private readonly AppConfig _config;
    private readonly Action _onSaved;
    public ObservableCollection<DeviceViewModel> Devices { get; } = new();
    public ObservableCollection<DeviceDefaultViewModel> Defaults { get; } = new();

    private HotkeyBinding _nextHotkey;
    private HotkeyBinding _prevHotkey;
    private HotkeyBinding _defaultsHotkey;

    public SettingsWindow(AudioDeviceService audioService, AppConfig config, Action onSaved)
    {
        _audioService = audioService;
        _config = config;
        _onSaved = onSaved;

        _nextHotkey = new HotkeyBinding { Modifiers = config.NextDeviceHotkey.Modifiers, Key = config.NextDeviceHotkey.Key };
        _prevHotkey = new HotkeyBinding { Modifiers = config.PreviousDeviceHotkey.Modifiers, Key = config.PreviousDeviceHotkey.Key };
        _defaultsHotkey = new HotkeyBinding { Modifiers = config.ApplyDefaultsHotkey.Modifiers, Key = config.ApplyDefaultsHotkey.Key };

        InitializeComponent();

        DeviceList.ItemsSource = Devices;
        RefreshDevices();

        NextHotkeyBox.Text = _nextHotkey.DisplayString;
        PrevHotkeyBox.Text = _prevHotkey.DisplayString;
        DefaultsHotkeyBox.Text = _defaultsHotkey.DisplayString;
        StartupCheckBox.IsChecked = StartupHelper.IsEnabled;
    }

    private void RefreshDevices()
    {
        var devices = _audioService.GetPlaybackDevices();
        var enabledIds = _config.EnabledDeviceIds;

        // Build list: enabled devices in config order first, then remaining
        var ordered = new List<DeviceViewModel>();
        foreach (var id in enabledIds)
        {
            var dev = devices.FirstOrDefault(d => d.Id == id);
            if (dev != null)
                ordered.Add(new DeviceViewModel { Id = dev.Id, DisplayName = dev.FriendlyName, IsEnabled = true, IsDefault = dev.IsDefault });
        }
        foreach (var dev in devices)
        {
            if (!enabledIds.Contains(dev.Id))
                ordered.Add(new DeviceViewModel { Id = dev.Id, DisplayName = dev.FriendlyName, IsEnabled = false, IsDefault = dev.IsDefault });
        }

        Devices.Clear();
        foreach (var d in ordered) Devices.Add(d);

        RefreshDefaults(devices);
    }

    private void RefreshDefaults(List<AudioDevice> devices)
    {
        var previousDefaults = Defaults
            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
            .GroupBy(d => d.Id)
            .ToDictionary(g => g.Key, g => g.Last());
        var configuredDefaults = _config.DeviceDefaults
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
            .GroupBy(d => d.DeviceId)
            .ToDictionary(g => g.Key, g => g.Last());
        string? currentDefaultId = devices.FirstOrDefault(d => d.IsDefault)?.Id;

        Defaults.Clear();
        foreach (var dev in devices)
        {
            int volume = 50;
            bool isPrimary = dev.Id == currentDefaultId;

            if (configuredDefaults.TryGetValue(dev.Id, out var configured))
            {
                volume = configured.Volume;
                isPrimary = configured.IsPrimary;
            }
            else if (previousDefaults.TryGetValue(dev.Id, out var previous))
            {
                volume = previous.Volume;
                isPrimary = previous.IsPrimary;
            }

            Defaults.Add(new DeviceDefaultViewModel
            {
                Id = dev.Id,
                DisplayName = dev.FriendlyName,
                Volume = volume,
                IsPrimary = isPrimary
            });
        }

        if (!Defaults.Any(d => d.IsPrimary) && Defaults.Count > 0)
            Defaults[0].IsPrimary = true;
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshDevices();

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        int idx = DeviceList.SelectedIndex;
        if (idx > 0)
        {
            Devices.Move(idx, idx - 1);
            DeviceList.SelectedIndex = idx - 1;
        }
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        int idx = DeviceList.SelectedIndex;
        if (idx >= 0 && idx < Devices.Count - 1)
        {
            Devices.Move(idx, idx + 1);
            DeviceList.SelectedIndex = idx + 1;
        }
    }

    private void OnHotkeyGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
            tb.Text = "Press a key combination...";
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore lone modifier keys
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var modifiers = Keyboard.Modifiers;
        var binding = new HotkeyBinding { Modifiers = modifiers, Key = key };

        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Text = binding.DisplayString;
            if ((string)tb.Tag == "Next")
            {
                _nextHotkey = binding;
            }
            else if ((string)tb.Tag == "Prev")
            {
                _prevHotkey = binding;
            }
            else
            {
                _defaultsHotkey = binding;
            }
        }
    }

    private void OnClearHotkey(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            var empty = new HotkeyBinding { Modifiers = ModifierKeys.None, Key = Key.None };
            if ((string)btn.Tag == "Next")
            {
                _nextHotkey = empty;
                NextHotkeyBox.Text = "";
            }
            else if ((string)btn.Tag == "Prev")
            {
                _prevHotkey = empty;
                PrevHotkeyBox.Text = "";
            }
            else
            {
                _defaultsHotkey = empty;
                DefaultsHotkeyBox.Text = "";
            }
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var hotkeys = new[]
        {
            ("Next", _nextHotkey),
            ("Previous", _prevHotkey),
            ("Apply Defaults", _defaultsHotkey)
        };

        for (int i = 0; i < hotkeys.Length; i++)
        {
            for (int j = i + 1; j < hotkeys.Length; j++)
            {
                if (!hotkeys[i].Item2.IsEmpty && !hotkeys[j].Item2.IsEmpty
                    && hotkeys[i].Item2.Modifiers == hotkeys[j].Item2.Modifiers
                    && hotkeys[i].Item2.Key == hotkeys[j].Item2.Key)
                {
                    MessageBox.Show($"{hotkeys[i].Item1} and {hotkeys[j].Item1} hotkeys cannot be the same.", "Hotkey Conflict",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }

        _config.EnabledDeviceIds = Devices.Where(d => d.IsEnabled).Select(d => d.Id).ToList();
        _config.NextDeviceHotkey = _nextHotkey;
        _config.PreviousDeviceHotkey = _prevHotkey;
        _config.ApplyDefaultsHotkey = _defaultsHotkey;
        _config.DeviceDefaults = Defaults.Select(d => new DeviceDefault
        {
            DeviceId = d.Id,
            Volume = d.Volume,
            IsPrimary = d.IsPrimary
        }).ToList();
        _config.StartWithWindows = StartupCheckBox.IsChecked == true;
        _config.Save();

        StartupHelper.SetEnabled(_config.StartWithWindows);

        _onSaved();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnPrimaryDefaultChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox { DataContext: DeviceDefaultViewModel selected }
            || !selected.IsPrimary)
        {
            return;
        }

        foreach (var deviceDefault in Defaults)
        {
            if (!ReferenceEquals(deviceDefault, selected))
                deviceDefault.IsPrimary = false;
        }
    }
}

public class DeviceViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public bool IsDefault { get; set; }

    private string _displayName = "";
    public string DisplayName
    {
        get => IsDefault ? $"{_displayName} (Default)" : _displayName;
        set { _displayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName))); }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DeviceDefaultViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";

    private int _volume = 50;
    public int Volume
    {
        get => _volume;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (_volume == clamped) return;
            _volume = clamped;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        }
    }

    private bool _isPrimary;
    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            if (_isPrimary == value) return;
            _isPrimary = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPrimary)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
