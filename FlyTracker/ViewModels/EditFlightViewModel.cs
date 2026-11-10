using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyTracker.Models;
using FlyTracker.Persistence;
using System.Globalization;

namespace FlyTracker.ViewModels;

public sealed partial class EditFlightViewModel : ObservableObject
{
    private string _error = "";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private readonly Flight original;
    private readonly FlightStore store;

    [ObservableProperty]
    public partial string Title { get; set; }
    [ObservableProperty]
    public partial string Note { get; set; }
    [ObservableProperty]
    public partial string StartTime { get; set; }
    [ObservableProperty]
    public partial string EndTime { get; set; }

    public string Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public event EventHandler? Saved;

    public EditFlightViewModel(Flight flight, FlightStore store)
    {
        if (flight.End is not long end)
            throw new ArgumentException("Редактировать можно только завершённый вылет.", nameof(flight));

        original = flight;
        this.store = store;
        Title = flight.Title;
        Note = flight.Note;
        StartTime = FormatTime(flight.Start);
        EndTime = FormatTime(end);
    }

    private static string FormatTime(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static bool ReadTime(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(text.Trim(), TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out value);

    [RelayCommand]
    private void Save()
    {
        if (!ReadTime(StartTime, out var start) || !ReadTime(EndTime, out var end))
        {
            Error = "Введите дату и время в указанном формате.";
            return;
        }

        try
        {
            store.Save(original with
            {
                Title = Title,
                Note = Note,
                Start = start.ToUnixTimeSeconds(),
                End = end.ToUnixTimeSeconds()
            });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return;
        }

        Error = "";
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
