using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FlyTracker;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyTracker");
    private FlightStore store = null!;
    private GameLogs? logs;
    private List<Flight> flights = [];
    private Flight? Selected => History.SelectedItem as Flight;
    private Flight? Active => flights.Find(f => f.End == null);
    private bool working;
    private bool switching;
    private bool refresh = true;
    private long lastScan;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Initialize();
        Closed += (_, _) => timer.Stop();
        timer.Tick += async (_, _) => await Tick();
    }

    private async Task Initialize()
    {
        try
        {
            store = new(Path.Combine(data, "flights.sqlite"));
            Reload();
            string setting = Path.Combine(data, "logs.path");
            var folder = File.Exists(setting) ? File.ReadAllText(setting) :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs", "Gamelogs");

            await LoadFolder(folder);
            timer.Start();
            await Tick();
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private async Task LoadFolder(string folder)
    {
        working = true;
        UpdateControls();
        Status.Text = "Чтение заголовков Gamelogs…";
        try
        {
            var source = new GameLogs(folder, store);
            var pilots = await Task.Run(source.Pilots);
            logs = source;
            File.WriteAllText(Path.Combine(data, "logs.path"), folder);
            Pilots.ItemsSource = pilots;
            Pilots.SelectedItem = Active?.Pilot ?? Selected?.Pilot;
            if (Pilots.SelectedIndex < 0 && pilots.Length > 0) Pilots.SelectedIndex = 0;
            Status.Text = pilots.Length == 0 ? "Журналы с именем пилота не найдены" : $"Gamelogs · пилотов: {pilots.Length}";
            refresh = true;
        }
        catch (Exception ex) { Status.Text = $"Папка журналов: {ex.Message}"; }
        finally { working = false; UpdateControls(); }
    }

    private void Reload(long? id = null)
    {
        id ??= Selected?.Id;
        flights = store.History();
        ApplyFilter(id ?? Active?.Id);
    }

    private void ApplyFilter(long? id)
    {
        switching = true;
        string query = Search.Text.Trim();
        var visible = flights.Where(f => f.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            f.Pilot.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        History.ItemsSource = visible;
        History.SelectedItem = visible.Find(f => f.Id == id) ?? visible.FirstOrDefault();
        switching = false;
        ShowSelection();
    }

    private void ShowSelection()
    {
        refresh = true;
        var flight = Selected;
        Heading.Text = flight?.Title ?? "Журнал вылетов";
        Notes.Text = flight?.Note ?? "";
        Notes.ToolTip = Notes.Text;
        Draw(flight == null ? [] : store.Events(flight));
        UpdateControls();
        ShowTime();
    }

    private void Draw(List<Observation> events)
    {
        int jumps = events.Count(e => e.Kind == "jump");
        Metrics.Text = $"Нанесено  {FlightReport.Damage(events, "out")}     Получено  {FlightReport.Damage(events, "in")}     Переходы  {(jumps == 0 ? "нет данных" : jumps)}";
        var lastJump = events.LastOrDefault(e => e.Kind == "jump");
        SystemSeen.Text = lastJump == null ? "Система · нет данных" : $"Последняя замеченная система · {lastJump.Detail} в {lastJump.Clock} UTC";
        Route.Text = "Маршрут   " + FlightReport.Route(events);
        Route.ToolTip = Route.Text;
        Feed.ItemsSource = events.AsEnumerable().Reverse().ToList();
        Targets.ItemsSource = events.Where(e => e.Damage.HasValue).GroupBy(e => e.Subject)
            .OrderByDescending(group => group.Count()).Select(group => new
            {
                Name = group.Key, Outgoing = FlightReport.Damage(group, "out"),
                Incoming = FlightReport.Damage(group, "in"), Hits = group.Count()
            }).ToList();
    }

    private void ShowTime()
    {
        if (Selected is not { } flight)
        {
            Dates.Text = "Выберите пилота и нажмите «Начать вылет».";
            return;
        }

        var start = DateTimeOffset.FromUnixTimeSeconds(flight.Start);
        var end = flight.End.HasValue ? DateTimeOffset.FromUnixTimeSeconds(flight.End.Value) : DateTimeOffset.UtcNow;
        var elapsed = end - start;
        var finish = flight.End == null ? "идёт запись" : end.ToString("dd.MM HH:mm:ss");
        Dates.Text = $"{flight.Pilot}   ·   {start:dd.MM.yyyy HH:mm:ss} — {finish} UTC   ·   {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private async Task Tick()
    {
        ShowTime();
        if (working || logs == null || (Selected == null && Active == null)) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!refresh && (now - lastScan < 4 || Active == null && now - Selected!.End > 60)) return;
        var selected = Selected;
        var active = Active;
        var source = logs;
        working = true;
        refresh = false;
        lastScan = now;
        try
        {
            var events = await Task.Run(() =>
            {
                if (active != null) source.Import(active);
                if (selected == null) return [];
                if (selected.Id != active?.Id) source.Import(selected);
                return store.Events(selected);
            });

            if (Selected == selected) Draw(events);
        }
        catch (Exception ex) { Status.Text = $"Чтение журналов: {ex.Message}"; refresh = true; }
        finally { working = false; UpdateControls(); }
    }

    private void UpdateControls()
    {
        StartButton.IsEnabled = Active == null && Pilots.SelectedItem != null;
        StopButton.IsEnabled = Active != null;
        Pilots.IsEnabled = Active == null;
        FolderButton.IsEnabled = !working;
        EditButton.IsEnabled = Selected?.End != null;
    }

    private void StartClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Pilots.SelectedItem is not string pilot) return;
            var id = store.Start(pilot, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Search.Clear();
            Reload(id);
            Status.Text = "Запись · " + pilot;
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private async void StopClick(object sender, RoutedEventArgs e)
    {
        if (Active is not { } flight) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now <= flight.Start) { Status.Text = "Запись началась в эту секунду. Завершите через секунду."; return; }
        try
        {
            store.Save(flight with { End = now });
            Search.Clear();
            Reload(flight.Id);
            Status.Text = "Отчёт сохранён";
            await Tick();
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { End: not null } flight) return;
        if (new EditWindow(flight, store) { Owner = this }.ShowDialog() == true) Reload(flight.Id);
    }

    private async void FolderClick(object sender, RoutedEventArgs e)
    {
        if (working) return;
        var dialog = new OpenFolderDialog { Title = "Выберите папку EVE Gamelogs" };
        if (dialog.ShowDialog(this) == true) await LoadFolder(dialog.FolderName);
    }

    private void FilterChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter(Selected?.Id);
    }

    private void FlightSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!switching && IsLoaded) ShowSelection();
    }
}

