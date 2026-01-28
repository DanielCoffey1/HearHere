# HearHere

A Windows system-tray utility for switching the default audio output device via global hotkeys.

## Build & Run

Requires .NET 8 SDK.

```
dotnet build src/HearHere/HearHere.csproj
dotnet run --project src/HearHere/HearHere.csproj
```

Or publish a self-contained exe:

```
dotnet publish src/HearHere/HearHere.csproj -c Release -r win-x64 --self-contained
```

## Usage

1. On launch the app minimizes to the system tray.
2. Right-click the tray icon to open settings, switch devices, or quit.
3. Double-click the tray icon to open settings.
4. In settings, check the devices you want in your switching rotation, reorder them with the up/down buttons, and configure hotkeys.
5. Default hotkeys: **Ctrl+Alt+F11** (next), **Ctrl+Alt+Shift+F11** (previous).

## File Locations

| What | Path |
|------|------|
| Config | `%AppData%\HearHere\config.json` |
| Logs | `%AppData%\HearHere\logs\app-YYYY-MM-DD.log` |

## How It Works

- **Device enumeration**: Uses the documented `IMMDeviceEnumerator` / `IMMDevice` Core Audio COM APIs.
- **Setting default device**: Uses the undocumented `IPolicyConfig` COM interface (GUID `f8679f50-850a-41cf-9c72-430f290290c8`), activated via `PolicyConfigClass` (GUID `870af99c-171d-4f9e-af0d-e63df40c2bc9`). This is the same approach used by SoundSwitch, EarTrumpet, and similar utilities. It sets the default for all three roles (Console, Multimedia, Communications).
- **Global hotkeys**: Registered via Win32 `RegisterHotKey` with an `HwndSource` message hook.
- **Device change notifications**: Registered via `IMMNotificationClient` callback on the enumerator.

## Known Limitations

- Sets all three audio roles (console, multimedia, communications) together. There is no separate "communications default" toggle.
- The `IPolicyConfig` interface is undocumented and could theoretically change in a future Windows update, though it has been stable since Windows Vista.
- No admin privileges required.
- Targets Windows 10/11. Not tested on older versions.
