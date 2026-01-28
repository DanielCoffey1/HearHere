using System.Windows;

namespace HearHere;

/// <summary>Hidden window that provides an HWND for global hotkey registration.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
