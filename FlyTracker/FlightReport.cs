namespace FlyTracker;

public static class FlightReport
{
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
