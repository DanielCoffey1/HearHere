using HearHere.Config;
using HearHere.Logging;

namespace HearHere.Audio;

/// <summary>
/// High-level switching logic: cycles through the user's enabled device
/// list in order, wrapping around.
/// </summary>
public sealed class DeviceSwitcher
{
    private readonly AudioDeviceService _service;
    private readonly AppConfig _config;

    public DeviceSwitcher(AudioDeviceService service, AppConfig config)
    {
        _service = service;
        _config = config;
    }

    /// <summary>Switch to next/previous enabled device. Returns the device switched to, or null on failure.</summary>
    public AudioDevice? Switch(bool forward)
    {
        var enabledIds = _config.EnabledDeviceIds;
        if (enabledIds.Count == 0)
        {
            Log.Write("No enabled devices configured.");
            return null;
        }

        var allDevices = _service.GetPlaybackDevices();
        // Build ordered list of devices that are both enabled and currently active
        var available = new List<AudioDevice>();
        foreach (var id in enabledIds)
        {
            var dev = allDevices.FirstOrDefault(d => d.Id == id);
            if (dev != null) available.Add(dev);
        }

        if (available.Count == 0)
        {
            Log.Write("No enabled devices are currently active.");
            return null;
        }

        string? currentId = _service.GetDefaultDeviceId();
        int currentIndex = available.FindIndex(d => d.Id == currentId);

        int nextIndex;
        if (currentIndex < 0)
        {
            // Current default isn't in our list, jump to first
            nextIndex = 0;
        }
        else
        {
            nextIndex = forward
                ? (currentIndex + 1) % available.Count
                : (currentIndex - 1 + available.Count) % available.Count;
        }

        var target = available[nextIndex];
        try
        {
            _service.SetDefaultDevice(target.Id);
            Log.Write($"Switched to: {target.FriendlyName}");
            return target;
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to switch to {target.FriendlyName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Switch to a specific enabled device by 1-based index.</summary>
    public AudioDevice? SwitchTo(int oneBasedIndex)
    {
        var enabledIds = _config.EnabledDeviceIds;
        if (oneBasedIndex < 1 || oneBasedIndex > enabledIds.Count) return null;

        var allDevices = _service.GetPlaybackDevices();
        string targetId = enabledIds[oneBasedIndex - 1];
        var target = allDevices.FirstOrDefault(d => d.Id == targetId);
        if (target == null) return null;

        try
        {
            _service.SetDefaultDevice(target.Id);
            Log.Write($"Switched to #{oneBasedIndex}: {target.FriendlyName}");
            return target;
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to switch to #{oneBasedIndex}: {ex.Message}");
            return null;
        }
    }
}
