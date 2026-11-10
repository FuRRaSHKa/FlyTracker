using FlyTracker.ViewModels;
using System.Windows;

namespace FlyTracker.Views;

public partial class EditWindow : Window
{
    public EditWindow(EditFlightViewModel editor)
    {
        InitializeComponent();
        DataContext = editor;
        editor.Saved += OnSaved;
        Closed += (_, _) => editor.Saved -= OnSaved;
    }

    private void OnSaved(object? sender, EventArgs e) => DialogResult = true;
}
