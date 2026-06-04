using System.Windows;
using H.NotifyIcon;
using Tidsro.Services;

namespace Tidsro;

public partial class App : Application
{
    private TaskbarIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _tray = TrayBuilder.Create(ShowMainWindow, Quit);
    }

    private void ShowMainWindow()
    {
        var win = MainWindow ??= new MainWindow();
        win.Show();
        win.WindowState = WindowState.Normal;
        win.Activate();
    }

    private void Quit()
    {
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
