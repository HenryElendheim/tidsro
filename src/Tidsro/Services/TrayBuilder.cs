using System.Windows.Controls;
using H.NotifyIcon;

namespace Tidsro.Services;

public static class TrayBuilder
{
    public static TaskbarIcon Create(Action onOpen, Action onQuit)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => onOpen();
        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => onQuit();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        var icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/icons/tidsro.ico"));
        icon.Freeze();   // immutable -> releases the underlying stream; safe for a lifetime-held tray icon

        var tray = new TaskbarIcon
        {
            ToolTipText = "Tidsro",
            ContextMenu = menu,
            IconSource = icon
        };
        tray.TrayLeftMouseUp += (_, _) => onOpen();
        tray.ForceCreate();
        return tray;
    }
}
