using System.Windows;
using System.Windows.Threading;

namespace HearHere.OSD;

public partial class OsdWindow : Window
{
    private readonly DispatcherTimer _timer;

    public OsdWindow(string deviceName)
    {
        InitializeComponent();
        DeviceNameText.Text = deviceName;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position in top-right corner of primary screen
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Top + 16;
        _timer.Start();
    }

    /// <summary>Show an OSD toast. Call from UI thread.</summary>
    public static void ShowToast(string deviceName)
    {
        var osd = new OsdWindow(deviceName);
        osd.Show();
    }
}
