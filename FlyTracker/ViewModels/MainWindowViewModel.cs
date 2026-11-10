using CommunityToolkit.Mvvm.ComponentModel;
using FlyTracker.Logs;
using FlyTracker.Models;
using FlyTracker.Persistence;
using FlyTracker.Settings;

namespace FlyTracker.ViewModels;

internal sealed class MainWindowViewModel : ObservableObject
{
    private string _status = "";
    private readonly FlightStore _store;
    private readonly FlightLogUpdater _updater;
    private readonly LogFolderSettings _settings;
    private bool _loadingFolder;

    public MainWindowViewModel(FlightStore store, FlightLogUpdater updater, LogFolderSettings settings)
    {
        _store = store;
        _updater = updater;
        _settings = settings;
        Recording = new FlightRecordingViewModel(store, id => Reload(id, clearSearch: true), SetStatus, TickAsync);
        History.SelectionChanged += ShowFlight;
    }

    public FlightHistoryViewModel History { get; } = new();
    public FlightDetailsViewModel Details { get; } = new();
    public FlightRecordingViewModel Recording { get; }

    public bool CanChangeFolder => !_loadingFolder && !_updater.IsScanning;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }


    public async Task InitializeAsync()
    {
        try
        {
            Reload();

            await LoadFolderAsync(_settings.Read());
        }
        catch (Exception ex) 
        {
            SetStatus(ex.Message); 
        }
    }

    public EditFlightViewModel? CreateEditor()
    {
        if (History.SelectedFlight is not { End: not null } flight)
            return null;

        var editor = new EditFlightViewModel(flight, _store);
        editor.Saved += (_, _) => Reload(flight.Id);
        return editor;
    }

    private void Reload(long? id = null, bool clearSearch = false)
    {
        var flights = _store.History();
        var active = flights.Find(flight => flight.End == null);
        Recording.SetActive(active);
        History.Reload(flights, id ?? History.SelectedFlight?.Id ?? active?.Id, clearSearch);
    }

    private void ShowFlight(Flight? flight)
    {
        _updater.RequestRefresh();
        Details.Show(flight, flight == null ? [] : _store.Events(flight));
    }

    public async Task LoadFolderAsync(string folder)
    {
        if (_loadingFolder)
            return;

        _loadingFolder = true;
        OnPropertyChanged(nameof(CanChangeFolder));
        SetStatus("Чтение заголовков Gamelogs…");

        try
        {
            var source = new GameLogs(folder, _store);
            var pilots = await Task.Run(source.Pilots);
            _settings.Save(folder);
            _updater.UseSource(source);
            Recording.SetPilots(pilots, History.SelectedFlight?.Pilot);
            SetStatus(pilots.Length == 0 ? "Журналы с именем пилота не найдены" : $"Gamelogs · пилотов: {pilots.Length}");
        }
        catch (Exception ex) { SetStatus($"Папка журналов: {ex.Message}"); }
        finally
        {
            _loadingFolder = false;
            OnPropertyChanged(nameof(CanChangeFolder));
        }
    }

    public async Task TickAsync()
    {
        Details.RefreshDuration();
        if (_loadingFolder)
            return;

        var flight = History.SelectedFlight;
        try
        {
            var scan = _updater.RefreshAsync(Recording.Active, flight);
            OnPropertyChanged(nameof(CanChangeFolder));
            var events = await scan;
            if (events != null && ReferenceEquals(History.SelectedFlight, flight))
                Details.SetReport(events);
        }
        catch (Exception ex) { SetStatus($"Чтение журналов: {ex.Message}"); }
        finally { OnPropertyChanged(nameof(CanChangeFolder)); }
    }

    private void SetStatus(string message)
    {
        Status = message;
    }
}
