using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using HearHere.Logging;

namespace HearHere.Config;

public sealed class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearHere");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    /// <summary>Ordered list of device IDs the user wants to cycle through.</summary>
    public List<string> EnabledDeviceIds { get; set; } = new();

    /// <summary>Hotkey for switching to the next output device.</summary>
    public HotkeyBinding NextDeviceHotkey { get; set; } = new()
    {
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt,
        Key = Key.F11
    };

    /// <summary>Hotkey for switching to the previous output device.</summary>
    public HotkeyBinding PreviousDeviceHotkey { get; set; } = new()
    {
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
        Key = Key.F11
    };

    public bool StartWithWindows { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to load config: {ex.Message}");
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(ConfigPath, json);
            Log.Write("Config saved.");
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to save config: {ex.Message}");
        }
    }
}
