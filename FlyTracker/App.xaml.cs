using FlyTracker.Logs;
using FlyTracker.Persistence;
using FlyTracker.Settings;
using FlyTracker.ViewModels;
using FlyTracker.Views;
using System.IO;
using System.Threading;
using System.Windows;

namespace FlyTracker;

public partial class App : Application
{
    private Mutex? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, @"Local\FlyTracker.Desktop", out var first);
        if (!first)
        {
            MessageBox.Show("FlyTracker уже открыт.", "FlyTracker");
            Shutdown();
            return;
        }

        base.OnStartup(e);
        try
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyTracker");
            var store = new FlightStore(Path.Combine(data, "flights.sqlite"));
            var model = new MainWindowViewModel(store, new FlightLogUpdater(store), new LogFolderSettings(data));
            MainWindow = new MainWindow(model);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FlyTracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
