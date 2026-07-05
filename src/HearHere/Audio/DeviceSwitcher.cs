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

    /// <summary>Applies configured default endpoint, device volumes, and mixer resets.</summary>
    public AudioDevice? ApplyDefaults()
    {
        var defaults = _config.DeviceDefaults
            .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
            .ToList();

        if (defaults.Count == 0)
        {
            Log.Write("No device defaults configured.");
            return null;
        }

        var allDevices = _service.GetPlaybackDevices();
        string? currentId = _service.GetDefaultDeviceId();
        var primaryDefault = defaults.FirstOrDefault(d => d.IsPrimary);
        if (primaryDefault == null)
        {
            Log.Write("No primary default device configured.");
            return null;
        }

        var target = allDevices.FirstOrDefault(d => d.Id == primaryDefault.DeviceId);
        if (target == null)
        {
            Log.Write("Primary default device is not currently active.");
            return null;
        }

        foreach (var deviceDefault in defaults)
        {
            if (allDevices.All(d => d.Id != deviceDefault.DeviceId))
            {
                Log.Write($"Skipping unavailable default device: {deviceDefault.DeviceId}");
                continue;
            }

            try
            {
                _service.SetDeviceVolume(deviceDefault.DeviceId, deviceDefault.Volume);
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to set default volume for {deviceDefault.DeviceId}: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(currentId) && allDevices.Any(d => d.Id == currentId))
        {
            try
            {
                _service.ResetMixerSessions(currentId);
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to reset current mixer sessions for {currentId}: {ex.Message}");
            }
        }

        try
        {
            _service.SetDefaultDevice(target.Id);
            if (currentId != target.Id)
            {
                try
                {
                    _service.ResetMixerSessions(target.Id);
                }
                catch (Exception ex)
                {
                    Log.Write($"Failed to reset primary mixer sessions for {target.Id}: {ex.Message}");
                }
            }

            Log.Write($"Applied defaults and switched to primary: {target.FriendlyName}");
            return target;
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to switch to primary default {target.FriendlyName}: {ex.Message}");
            return null;
        }
    }
}
