using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HearHere.Logging;

namespace HearHere.Hotkeys;

/// <summary>
/// Registers global hotkeys via Win32 RegisterHotKey and listens for
/// WM_HOTKEY messages through an HwndSource message hook.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        // Ensure the HWND exists (the window may be hidden)
        if (helper.Handle == IntPtr.Zero)
            helper.EnsureHandle();
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>
    /// Register a global hotkey. Returns the registration id, or -1 on failure.
    /// </summary>
    public int Register(ModifierKeys modifiers, Key key, Action callback)
    {
        if (key == Key.None) return -1;

        uint fsModifiers = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) fsModifiers |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) fsModifiers |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) fsModifiers |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) fsModifiers |= 0x0008;
        fsModifiers |= 0x4000; // MOD_NOREPEAT

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        int id = _nextId++;

        if (!RegisterHotKey(_hwnd, id, fsModifiers, vk))
        {
            int err = Marshal.GetLastWin32Error();
            Log.Write($"RegisterHotKey failed for id={id} ({modifiers}+{key}), error={err}");
            return -1;
        }

        _handlers[id] = callback;
        Log.Write($"Hotkey registered: id={id} {modifiers}+{key}");
        return id;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _handlers.Clear();
        _nextId = 1;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var callback))
            {
                callback();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
