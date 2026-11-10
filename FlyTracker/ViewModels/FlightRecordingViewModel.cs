using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyTracker.Models;
using FlyTracker.Persistence;

namespace FlyTracker.ViewModels;

internal sealed partial class FlightRecordingViewModel : ObservableObject
{
    private string[] _pilots = [];
    private readonly FlightStore _store;
    private readonly Action<long> _reveal;
    private readonly Action<string> _setStatus;
    private readonly Func<Task> _refresh;

    public FlightRecordingViewModel(FlightStore store, Action<long> reveal, Action<string> setStatus, Func<Task> refresh)
    {
        _store = store;
        _reveal = reveal;
        _setStatus = setStatus;
        _refresh = refresh;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoosePilot))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    public partial Flight? Active { get; private set; }

    public bool CanChoosePilot => Active == null;

    public string[] Pilots
    {
        get => _pilots;
        private set => SetProperty(ref _pilots, value);
    }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string? SelectedPilot { get; set; }

    public void SetActive(Flight? flight) => Active = flight;

    public void SetPilots(string[] pilots, string? selectedFlightPilot)
    {
        var preferred = Active?.Pilot ?? selectedFlightPilot;
        Pilots = pilots;
        SelectedPilot = pilots.Contains(preferred) ? preferred : pilots.FirstOrDefault();
    }

    private bool CanStart() => CanChoosePilot && SelectedPilot != null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (!CanChoosePilot || SelectedPilot is not string pilot)
            return;
        
        try
        {
            var id = _store.Start(pilot, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _reveal(id);
            _setStatus("Запись · " + pilot);
        }
        catch (Exception ex) { _setStatus(ex.Message); }
    }

    private bool CanStop() => Active != null;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (Active is not { } flight)
            return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now <= flight.Start)
        {
            _setStatus("Запись началась в эту секунду. Завершите через секунду.");
            return;
        }

        try
        {
            _store.Save(flight with { End = now });
            _reveal(flight.Id);
            _setStatus("Отчёт сохранён");
            await _refresh();
        }
        catch (Exception ex) { _setStatus(ex.Message); }
    }
}
