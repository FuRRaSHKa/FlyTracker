using FlyTracker.ViewModels;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FlyTracker.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly MainWindowViewModel _model;

    internal MainWindow(MainWindowViewModel model)
    {
        InitializeComponent();
        
        this._model = model;
        DataContext = model;
        
        Loaded += OnLoaded;
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += async (_, _) => await model.TickAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _model.InitializeAsync();
        
        if (!IsLoaded)
            return;
        
        _timer.Start();
        await _model.TickAsync();
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (_model.CreateEditor() is { } editor)
            new EditWindow(editor) { Owner = this }.ShowDialog();
    }

    private async void FolderClick(object sender, RoutedEventArgs e)
    {
        if (!_model.CanChangeFolder)
            return;

        var dialog = new OpenFolderDialog { Title = "Выберите папку EVE Gamelogs" };
        if (dialog.ShowDialog(this) == true)
            await _model.LoadFolderAsync(dialog.FolderName);
    }
}
