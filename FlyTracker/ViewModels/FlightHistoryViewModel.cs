using CommunityToolkit.Mvvm.ComponentModel;
using FlyTracker.Models;

namespace FlyTracker.ViewModels;

internal sealed partial class FlightHistoryViewModel : ObservableObject
{
    private List<Flight> _flights = [];
    private Flight? _selectedFlight;
    private string _search = "";

    public event Action<Flight?>? SelectionChanged;
    public List<Flight> Items { get; private set; } = [];

    public Flight? SelectedFlight
    {
        get => _selectedFlight;
        set
        {
            if (ReferenceEquals(_selectedFlight, value))
                return;

            _selectedFlight = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(value);
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
                ApplyFilter(SelectedFlight?.Id);
        }
    }

    public void Reload(List<Flight> flights, long? selectedId, bool clearSearch)
    {
        _flights = flights;
        if (clearSearch)
        {
            SetProperty(ref _search, "", nameof(Search));
        }

        ApplyFilter(selectedId);
    }

    private void ApplyFilter(long? id)
    {
        var query = _search.Trim();
        Items = _flights.Where(f => f.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            f.Pilot.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var next = Items.Find(f => f.Id == id) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(Items));
        SelectedFlight = next;
    }
}
