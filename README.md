# HearHere

HearHere is a Windows system-tray utility for switching playback devices, restoring preferred output volumes, and resetting active volume mixer sessions with global hotkeys.

It is built for the common "speakers most of the time, headset while gaming or chatting" workflow: switch outputs quickly, make temporary volume/mixer changes, then press one hotkey to return to your normal setup.

## Features

- Runs quietly from the Windows system tray.
- Switches between selected playback devices in a custom order.
- Supports global hotkeys for next device, previous device, and apply defaults.
- Shows an on-screen display when switching devices or applying defaults.
- Lets you choose a primary/default restore device.
- Stores a preferred master volume for each playback output.
- Applies defaults by:
  - setting saved output volumes,
  - resetting active volume mixer sessions on the current output,
  - switching to the configured primary output,
  - resetting active volume mixer sessions on the primary output.
- Optional startup with Windows.
- No administrator privileges required.

## Usage

1. Launch HearHere. It will appear in the system tray.
2. Right-click the tray icon to open settings, switch devices, or quit.
3. Double-click the tray icon to open settings.
4. In the `Devices` tab, check the playback devices you want in your switch rotation and reorder them with `Up` / `Down`.
5. In the `Defaults` tab, set each output's restore volume and choose one primary output.
6. In `Hotkeys`, configure:
   - `Next Device`
   - `Previous Device`
   - `Apply Defaults`
7. Press `Apply Defaults` after temporary gaming/chat/session changes to return to your normal output, output volume, and active mixer levels.

Default hotkeys:

| Action | Hotkey |
| --- | --- |
| Next Device | `Ctrl+Alt+F11` |
| Previous Device | `Ctrl+Alt+Shift+F11` |
| Apply Defaults | `Ctrl+Alt+F12` |

## Example

If your normal output is `LG ULTRAGEAR` at volume `6`, and your gaming headset is `ROG CETRA` at volume `50`:

1. Set `LG ULTRAGEAR` to volume `6` in the `Defaults` tab.
2. Set `ROG CETRA` to volume `50`.
3. Mark `LG ULTRAGEAR` as the primary default.
4. Use the switch hotkey to move to `ROG CETRA` while gaming.
5. Temporarily raise headset volume or adjust the Windows volume mixer.
6. Press the `Apply Defaults` hotkey when done.

HearHere restores the configured output volumes, switches back to `LG ULTRAGEAR`, and resets active mixer sessions back to max/unmuted.

## File Locations

| What | Path |
| --- | --- |
| Config | `%AppData%\HearHere\config.json` |
| Logs | `%AppData%\HearHere\logs\app-YYYY-MM-DD.log` |
| Debug build output | `src\HearHere\bin\Debug\net8.0-windows\win-x64\` |
| Published app | `publish\HearHere.exe` |
| Installer | `publish\HearHereSetup.exe` |

## Build & Run

Requires the .NET 8 SDK.

```powershell
dotnet build HearHere.sln
dotnet run --project src\HearHere\HearHere.csproj
```

Publish a self-contained executable:

```powershell
dotnet publish src\HearHere\HearHere.csproj -c Release -o publish
```

The project is configured as a Windows self-contained single-file publish, so the release output includes an installable `HearHere.exe`.

## Building the Installer

The installer uses [Inno Setup](https://jrsoftware.org/isdownload.php).

One-command build:

```powershell
.\build.bat
```

Manual build:

```powershell
dotnet publish src\HearHere\HearHere.csproj -c Release -o publish
iscc installer\HearHere.iss
```

The installer is written to:

```text
publish\HearHereSetup.exe
```

The installer:

- installs `HearHere.exe` under Program Files,
- creates Start Menu shortcuts,
- optionally starts HearHere with Windows,
- can launch HearHere after installation,
- installs per-user and does not require admin rights.

## How It Works

- Device enumeration uses the documented `IMMDeviceEnumerator` / `IMMDevice` Core Audio COM APIs.
- Default device switching uses the undocumented but widely used `IPolicyConfig` COM interface. HearHere sets the Console, Multimedia, and Communications roles together.
- Endpoint volume restore uses `IAudioEndpointVolume`.
- Volume mixer reset uses `IAudioSessionManager2` and `ISimpleAudioVolume` for currently active audio sessions on the live output and restored primary output.
- Global hotkeys use Win32 `RegisterHotKey` with a hidden WPF window message hook.
- Device change notifications use `IMMNotificationClient`.

## Known Limitations

- Windows exposes mixer controls for active/current audio sessions. HearHere resets sessions that currently exist in the mixer; it cannot reset a future session before an app creates it.
- Some apps manage their own audio routing or recreate sessions after the reset.
- All three Windows audio roles are switched together. There is no separate communications-device setting.
- `IPolicyConfig` is undocumented. It has been stable for many Windows releases, but Microsoft does not officially support it.
- Targets Windows 10/11.
