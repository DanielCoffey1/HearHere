using System.IO;

namespace HearHere.Logging;

/// <summary>Simple day-rolling file logger.</summary>
public static class Log
{
    private static readonly object Lock = new();
    private static readonly string LogDir;
    private static string _currentDate = "";
    private static StreamWriter? _writer;

    static Log()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HearHere", "logs");
        Directory.CreateDirectory(LogDir);
    }

    public static void Write(string message)
    {
        lock (Lock)
        {
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (today != _currentDate)
                {
                    _writer?.Dispose();
                    _currentDate = today;
                    string path = Path.Combine(LogDir, $"app-{today}.log");
                    _writer = new StreamWriter(path, append: true) { AutoFlush = true };
                }
                _writer!.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch { /* logging must never crash the app */ }
        }
    }
}
