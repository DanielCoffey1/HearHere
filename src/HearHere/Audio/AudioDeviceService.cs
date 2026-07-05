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
    private const uint ClsCtxAll = 23;
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

    /// <summary>Sets a playback endpoint master volume as a percentage from 0 to 100 and unmutes it.</summary>
    public void SetDeviceVolume(string deviceId, int volumePercent)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        var endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
        var eventContext = Guid.Empty;

        int hr = _enumerator.GetDevice(deviceId, out var device);
        if (hr != 0)
            throw new COMException($"GetDevice failed (hr=0x{hr:X8})", hr);

        try
        {
            hr = device.Activate(ref endpointVolumeId, ClsCtxAll, IntPtr.Zero, out var endpointObj);
            if (hr != 0)
                throw new COMException($"IAudioEndpointVolume activation failed (hr=0x{hr:X8})", hr);

            var endpointVolume = (IAudioEndpointVolume)endpointObj;
            try
            {
                float scalar = volumePercent / 100f;
                int volumeHr = endpointVolume.SetMasterVolumeLevelScalar(scalar, ref eventContext);
                int muteHr = endpointVolume.SetMute(false, ref eventContext);
                if (volumeHr < 0)
                    throw new COMException($"SetMasterVolumeLevelScalar failed (hr=0x{volumeHr:X8})", volumeHr);
                if (muteHr < 0)
                    throw new COMException($"SetMute failed (hr=0x{muteHr:X8})", muteHr);

                Log.Write($"Volume for {deviceId} set to {volumePercent}%. SetMute hr=0x{muteHr:X8}.");
            }
            finally
            {
                Marshal.ReleaseComObject(endpointVolume);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    /// <summary>Resets currently active app mixer sessions on a playback endpoint to 100% and unmuted.</summary>
    public int ResetMixerSessions(string deviceId)
    {
        var sessionManagerId = typeof(IAudioSessionManager2).GUID;
        var eventContext = Guid.Empty;

        int hr = _enumerator.GetDevice(deviceId, out var device);
        if (hr != 0)
            throw new COMException($"GetDevice failed (hr=0x{hr:X8})", hr);

        try
        {
            hr = device.Activate(ref sessionManagerId, ClsCtxAll, IntPtr.Zero, out var managerObj);
            if (hr != 0)
                throw new COMException($"IAudioSessionManager2 activation failed (hr=0x{hr:X8})", hr);

            var manager = (IAudioSessionManager2)managerObj;
            try
            {
                hr = manager.GetSessionEnumerator(out var sessionEnumerator);
                if (hr != 0)
                    throw new COMException($"GetSessionEnumerator failed (hr=0x{hr:X8})", hr);

                try
                {
                    int countHr = sessionEnumerator.GetCount(out int count);
                    if (countHr != 0)
                        throw new COMException($"GetCount failed (hr=0x{countHr:X8})", countHr);

                    int resetCount = 0;
                    int failedCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int sessionHr = sessionEnumerator.GetSession(i, out var session);
                        if (sessionHr != 0)
                        {
                            Log.Write($"GetSession({i}) failed for {deviceId}: 0x{sessionHr:X8}");
                            failedCount++;
                            continue;
                        }

                        try
                        {
                            if (TryGetSimpleAudioVolume(session, out var volume))
                            {
                                try
                                {
                                    int volumeHr = volume.SetMasterVolume(1.0f, ref eventContext);
                                    int muteHr = volume.SetMute(false, ref eventContext);
                                    if (volumeHr >= 0 && muteHr >= 0)
                                    {
                                        resetCount++;
                                    }
                                    else
                                    {
                                        Log.Write($"Mixer reset failed for session {i} on {deviceId}: volume=0x{volumeHr:X8} mute=0x{muteHr:X8}");
                                        failedCount++;
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(volume);
                                }
                            }
                            else
                            {
                                Log.Write($"Session {i} on {deviceId} does not expose ISimpleAudioVolume.");
                                failedCount++;
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(session);
                        }
                    }

                    Log.Write($"Reset {resetCount}/{count} mixer sessions for {deviceId}. Failures: {failedCount}.");
                    return resetCount;
                }
                finally
                {
                    Marshal.ReleaseComObject(sessionEnumerator);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(manager);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static bool TryGetSimpleAudioVolume(object session, out ISimpleAudioVolume volume)
    {
        volume = null!;
        IntPtr sessionUnknown = IntPtr.Zero;
        IntPtr volumeUnknown = IntPtr.Zero;
        var volumeId = typeof(ISimpleAudioVolume).GUID;

        try
        {
            sessionUnknown = Marshal.GetIUnknownForObject(session);
            int hr = Marshal.QueryInterface(sessionUnknown, ref volumeId, out volumeUnknown);
            if (hr != 0 || volumeUnknown == IntPtr.Zero)
                return false;

            volume = (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(volumeUnknown);
            return true;
        }
        finally
        {
            if (volumeUnknown != IntPtr.Zero)
                Marshal.Release(volumeUnknown);
            if (sessionUnknown != IntPtr.Zero)
                Marshal.Release(sessionUnknown);
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
