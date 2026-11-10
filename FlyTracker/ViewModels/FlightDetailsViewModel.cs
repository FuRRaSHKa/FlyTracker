using CommunityToolkit.Mvvm.ComponentModel;
using FlyTracker.Models;
using FlyTracker.Reports;

namespace FlyTracker.ViewModels;

internal sealed class FlightDetailsViewModel : ObservableObject
{
    private FlightReport _report = FlightReport.Build([]);
    private Flight? _flight;

    public bool CanEdit => _flight?.End != null;
    public string? Note => _flight?.Note;

    public FlightReport Report
    {
        get => _report;
        private set
        {
            if (!SetProperty(ref _report, value))
                return;

            OnPropertyChanged(nameof(Metrics));
            OnPropertyChanged(nameof(SystemSeen));
            OnPropertyChanged(nameof(Route));
        }
    }

    public string Heading => _flight?.Title ?? "Журнал вылетов";
    public string Metrics => $"Нанесено  {Report.Outgoing}     Получено  {Report.Incoming}     Переходы  {(Report.Jumps == 0 ? "нет данных" : Report.Jumps)}";
    public string SystemSeen => Report.LastJump is { } jump
        ? $"Последняя замеченная система · {jump.Detail} в {jump.Clock} UTC" : "Система · нет данных";
    public string Route => "Маршрут   " + Report.RouteText;

    public string Dates
    {
        get
        {
            if (_flight is not { } flight)
                return "Выберите пилота и нажмите «Начать вылет».";
            var start = DateTimeOffset.FromUnixTimeSeconds(flight.Start);
            var end = flight.End.HasValue ? DateTimeOffset.FromUnixTimeSeconds(flight.End.Value) : DateTimeOffset.UtcNow;
            var elapsed = end - start;
            var finish = flight.End == null ? "идёт запись" : end.ToString("dd.MM HH:mm:ss");
            return $"{flight.Pilot}   ·   {start:dd.MM.yyyy HH:mm:ss} — {finish} UTC   ·   {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }

    public void Show(Flight? flight, IReadOnlyList<Observation> events)
    {
        _flight = flight;
        SetReport(events);
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(Dates));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(CanEdit));
    }

    public void RefreshDuration() => OnPropertyChanged(nameof(Dates));

    public void SetReport(IReadOnlyList<Observation> events)
    {
        Report = FlightReport.Build(events);
    }
}
