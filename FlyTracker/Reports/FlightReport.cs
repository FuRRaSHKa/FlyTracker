using FlyTracker.Models;

namespace FlyTracker.Reports;

public sealed record FlightReport(
    string Outgoing, string Incoming, int Jumps, Observation? LastJump, string RouteText,
    IReadOnlyList<Observation> Feed, IReadOnlyList<TargetSummary> Targets)
{
    public static FlightReport Build(IReadOnlyList<Observation> events)
    {
        var targets = events.Where(e => e.Damage.HasValue)
            .GroupBy(e => e.Subject)
            .Select(group => new TargetSummary(group.Key, Damage(group, "out"), Damage(group, "in"), group.Count()))
            .OrderByDescending(target => target.Hits)
            .ToArray();

        return new FlightReport(
            Damage(events, "out"), Damage(events, "in"), events.Count(e => e.Kind == "jump"),
            events.LastOrDefault(e => e.Kind == "jump"), Route(events), events.Reverse().ToArray(), targets);
    }

    public static string Damage(IEnumerable<Observation> items, string direction)
    {
        var values = items.Where(e => e.Kind == direction).Select(e => (long)e.Damage!.Value).ToList();
        return values.Count == 0 ? "нет данных" : values.Sum().ToString("N0");
    }

    public static string Route(IEnumerable<Observation> items)
    {
        var stops = new List<string>();
        foreach (var jump in items.Where(e => e.Kind == "jump"))
        {
            if (stops.Count == 0) stops.Add(jump.Subject);
            else if (stops[^1] != jump.Subject) stops.Add("…  " + jump.Subject);
            stops.Add(jump.Detail);
        }

        return stops.Count == 0 ? "нет данных" : string.Join("  →  ", stops);
    }
}
