namespace FlyTracker.Models;

public record Flight(long Id, string Pilot, long Start, long? End, string Title, string Note)
{
    public string Caption => $"{DateTimeOffset.FromUnixTimeSeconds(Start):dd.MM HH:mm}  ·  {Title}";
}
