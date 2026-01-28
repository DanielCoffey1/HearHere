using System.Runtime.InteropServices;

namespace HearHere.Interop;

// ─────────────────────────────────────────────────────────────────────
// Windows Core Audio COM interop definitions.
//
// To *enumerate* audio devices we use MMDeviceEnumerator / IMMDeviceEnumerator
// which is the public, documented Core Audio API.
//
// To *set* the default audio device we use the undocumented but widely-used
// IPolicyConfig COM interface.  Microsoft does not expose a public API for
// changing the default endpoint; every major audio-switcher (SoundSwitch,
// AudioSwitch, EarTrumpet, etc.) uses this same COM interface.
//
// The GUID below (568b9108-…) is for the Windows 10/11 variant of
// IPolicyConfig (sometimes called IPolicyConfigVista in older code).
// It has been stable across every Windows 10 and 11 release to date.
// ─────────────────────────────────────────────────────────────────────

#region Enums

/// <summary>Data-flow direction.</summary>
internal enum EDataFlow
{
    eRender = 0,   // playback
    eCapture = 1,
    eAll = 2
}

/// <summary>Device role.</summary>
internal enum ERole
{
    eConsole = 0,       // games / system sounds
    eMultimedia = 1,    // music / movies
    eCommunications = 2 // voice chat
}

/// <summary>Device state mask for EnumAudioEndpoints.</summary>
[Flags]
internal enum DeviceState : uint
{
    Active = 0x00000001,
    Disabled = 0x00000002,
    NotPresent = 0x00000004,
    Unplugged = 0x00000008,
    All = 0x0000000F
}

#endregion

#region Property keys

/// <summary>PKEY_Device_FriendlyName  {a45c254e-df1c-4efd-8020-67d146a850e0},14</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid fmtid;
    public uint pid;

    public static readonly PropertyKey PKEY_Device_FriendlyName =
        new() { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr data1;
    public IntPtr data2;

    public string? AsString()
    {
        if (vt == 31) // VT_LPWSTR
            return Marshal.PtrToStringUni(data1);
        return null;
    }

    public void Clear()
    {
        PropVariantClear(ref this);
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);
}

#endregion

#region IMMDevice interfaces

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate([In] ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

    [PreserveSig]
    int GetState(out uint pdwState);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint pcDevices);

    [PreserveSig]
    int Item(uint nDevice, out IMMDevice ppDevice);
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask, out IMMDeviceCollection ppDevices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorClass { }

[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}

#endregion

#region IPropertyStore

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint cProps);

    [PreserveSig]
    int GetAt(uint iProp, out PropertyKey pkey);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant propvar);

    [PreserveSig]
    int Commit();
}

#endregion

#region IPolicyConfig – used to SET the default audio endpoint

// ─────────────────────────────────────────────────────────────────────
// IPolicyConfig is an *undocumented* COM interface that has been
// present in Windows Vista through Windows 11.  There is no public
// Microsoft SDK header for it; the GUIDs and vtable layout below are
// reverse-engineered and used by every popular audio-switcher utility.
//
// The class GUID {870af99c-171d-4f9e-af0d-e63df40c2bc9} activates
// the PolicyConfig object.  The interface GUID {f8679f50-850a-41cf-
// 9c72-430f290290c8} is the "IPolicyConfigVista" variant which works
// on both Windows 10 and 11.
//
// We only call SetDefaultEndpoint(deviceId, role) which is at vtable
// slot index 12 in this interface layout.
// ─────────────────────────────────────────────────────────────────────

[ComImport]
[Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    // We must declare all vtable slots before SetDefaultEndpoint so the
    // CLR COM interop knows the correct offset.  We don't call these
    // methods, so they are just placeholder signatures.

    [PreserveSig] int GetMixFormat(string a, IntPtr b);
    [PreserveSig] int GetDeviceFormat(string a, int b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(string a);
    [PreserveSig] int SetDeviceFormat(string a, IntPtr b, IntPtr c);
    [PreserveSig] int GetProcessingPeriod(string a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod(string a, IntPtr b);
    [PreserveSig] int GetShareMode(string a, IntPtr b);
    [PreserveSig] int SetShareMode(string a, IntPtr b);
    [PreserveSig] int GetPropertyValue(string a, int b, PropertyKey c, IntPtr d);
    [PreserveSig] int SetPropertyValue(string a, int b, PropertyKey c, IntPtr d);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility(string a, int b);
}

[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClass { }

#endregion
