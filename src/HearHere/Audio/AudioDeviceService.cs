using System.Runtime.InteropServices;
using HearHere.Interop;
using HearHere.Logging;

namespace HearHere.Audio;

/// <summary>
/// Core audio service: enumerates playback devices, detects changes,
/// and sets the default output endpoint via PolicyConfig COM interop.
/// </summary>
public sealed class AudioDeviceService : IMMNotificationClient, IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private bool _disposed;

    /// <summary>Raised on the thread-pool when devices are added/removed/changed.</summary>
    public event Action? DevicesChanged;

    /// <summary>Raised when the default device changes.</summary>
    public event Action<string>? DefaultDeviceChanged;

    public AudioDeviceService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    /// <summary>Returns all active playback devices.</summary>
    public List<AudioDevice> GetPlaybackDevices()
    {
        var result = new List<AudioDevice>();

        string? defaultId = null;
        try
        {
            if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var defDev) == 0)
            {
                defDev.GetId(out defaultId);
                Marshal.ReleaseComObject(defDev);
            }
        }
        catch { /* no default device */ }

        int hr = _enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.Active, out var collection);
        if (hr != 0) return result;

        collection.GetCount(out uint count);
        for (uint i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            try
            {
                device.GetId(out string id);
                string name = GetFriendlyName(device) ?? id;
                result.Add(new AudioDevice
                {
                    Id = id,
                    FriendlyName = name,
                    IsDefault = id == defaultId
                });
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        Marshal.ReleaseComObject(collection);
        return result;
    }

    /// <summary>Returns the current default playback device ID, or null.</summary>
    public string? GetDefaultDeviceId()
    {
        try
        {
            if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var dev) == 0)
            {
                dev.GetId(out string id);
                Marshal.ReleaseComObject(dev);
                return id;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Sets the default playback device for Console and Multimedia roles.
    /// Uses the undocumented IPolicyConfig COM interface (see ComInterop.cs).
    /// </summary>
    public void SetDefaultDevice(string deviceId)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfigClass();
        try
        {
            int hr1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
            int hr2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            // Optionally set communications role too
            int hr3 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);

            if (hr1 != 0 || hr2 != 0)
            {
                Log.Write($"SetDefaultEndpoint failed: console=0x{hr1:X8} multimedia=0x{hr2:X8} comms=0x{hr3:X8}");
                throw new COMException($"SetDefaultEndpoint failed (hr=0x{hr1:X8})", hr1);
            }

            Log.Write($"Default device set to {deviceId}");
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }

    private static string? GetFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(0 /* STGM_READ */, out var store) != 0) return null;
        try
        {
            var key = PropertyKey.PKEY_Device_FriendlyName;
            if (store.GetValue(ref key, out var pv) == 0)
            {
                string? name = pv.AsString();
                pv.Clear();
                return name;
            }
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    #region IMMNotificationClient

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, uint newState) => DevicesChanged?.Invoke();
    void IMMNotificationClient.OnDeviceAdded(string deviceId) => DevicesChanged?.Invoke();
    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => DevicesChanged?.Invoke();
    void IMMNotificationClient.OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
    {
        if (flow == EDataFlow.eRender && role == ERole.eMultimedia)
            DefaultDeviceChanged?.Invoke(defaultDeviceId);
    }
    void IMMNotificationClient.OnPropertyValueChanged(string deviceId, PropertyKey key) { }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _enumerator.UnregisterEndpointNotificationCallback(this); } catch { }
        Marshal.ReleaseComObject(_enumerator);
    }
}
