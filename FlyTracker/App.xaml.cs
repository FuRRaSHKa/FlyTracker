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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
