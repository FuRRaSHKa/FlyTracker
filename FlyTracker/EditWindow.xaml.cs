using System.Globalization;
using System.Windows;

namespace FlyTracker;

public partial class EditWindow : Window
{
    private readonly Flight flight;
    private readonly FlightStore store;
    internal const string Format = "yyyy-MM-dd HH:mm:ss";

    public EditWindow(Flight flight, FlightStore store)
    {
        InitializeComponent();
        this.flight = flight;
        this.store = store;
        FlightName.Text = flight.Title;
        Note.Text = flight.Note;
        StartTime.Text = DateTimeOffset.FromUnixTimeSeconds(flight.Start).ToString(Format);
        EndTime.Text = DateTimeOffset.FromUnixTimeSeconds(flight.End!.Value).ToString(Format);
    }

    private void SaveClick(object sender, RoutedEventArgs args)
    {
        try
        {
            long ReadTime(string text) => DateTimeOffset.ParseExact(text.Trim(), Format,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUnixTimeSeconds();

            store.Save(flight with { Start = ReadTime(StartTime.Text), End = ReadTime(EndTime.Text),
                Title = FlightName.Text, Note = Note.Text });

            DialogResult = true;
        }
        catch (FormatException) { Error.Text = "Введите дату и время в указанном формате."; }
        catch (Exception ex) { Error.Text = ex.Message; }
    }
}
