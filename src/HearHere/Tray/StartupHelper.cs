using Microsoft.Win32;
using HearHere.Logging;

namespace HearHere.Tray;

/// <summary>Manages the "Start with Windows" HKCU\Run registry entry.</summary>
public static class StartupHelper
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HearHere";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = Environment.ProcessPath ?? "";
                key.SetValue(AppName, $"\"{exePath}\"");
                Log.Write("Startup entry added.");
            }
            else
            {
                key.DeleteValue(AppName, false);
                Log.Write("Startup entry removed.");
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to update startup: {ex.Message}");
        }
    }
}
