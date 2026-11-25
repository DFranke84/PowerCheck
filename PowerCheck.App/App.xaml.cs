using System.Windows;

namespace PowerCheck.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PowerCheck.exe --screenshots <folder>: draws each tab off-screen into PNG files for the README, then exits.
        if (e.Args.Length >= 2 && e.Args[0].Equals("--screenshots", StringComparison.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DeviceStore.NoPictures = true; // product pictures belong to their makers
            try { await MainWindow_ForScreenshots().MakeScreenshots(e.Args[1]); }
            finally { Shutdown(); }
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    private static MainWindow MainWindow_ForScreenshots() => new()
    {
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -20000,
        Top = 0,
        ShowInTaskbar = false,
        ShowActivated = false,
    };
}
