using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace FlyTracker;

public record Observation(long Time, string Kind, string Subject, string Detail, int? Damage)
{
    public string Clock => DateTimeOffset.FromUnixTimeSeconds(Time).ToString("HH:mm:ss");
    public string Label => Kind switch
    {
        "jump" => "Переход",
        "out" => "Нанесено",
        "in" => "Получено",
        _ => "Бой · без разбора"
    };

    public string Value => Damage?.ToString("N0") ?? "—";
}

public static class LogParser
{
    private static readonly Regex Entry = new(@"^\[ (?<time>[\d.]+ [\d:]+) \] \((?<type>[^)]*)\) (?<body>.*)$", RegexOptions.Compiled);
    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Jump = new(@"^(?:Jumping from (.+?) to (.+)|Осуществляется прыжок из (.+?) в (.+))$", RegexOptions.Compiled);
    private static readonly Regex Hit = new(@"^(\d+) (to|from|на|из) (.+?)(?: - (.*))?$", RegexOptions.Compiled);

    public static Observation? Parse(string line)
    {
        var entry = Entry.Match(line);
        if (!entry.Success || !DateTimeOffset.TryParseExact(entry.Groups["time"].Value,
            "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            return null;

        var type = entry.Groups["type"].Value;
        if (type != "combat" && type != "None") return null;
        var body = WebUtility.HtmlDecode(Tags.Replace(entry.Groups["body"].Value, "")).Trim();
        var time = timestamp.ToUnixTimeSeconds();
        if (type == "None")
        {
            var jump = Jump.Match(body);
            if (!jump.Success) return null;
            var index = jump.Groups[1].Success ? 1 : 3;
            return new(time, "jump", jump.Groups[index].Value.TrimEnd('*'), jump.Groups[index + 1].Value.TrimEnd('*'), null);
        }

        var hit = Hit.Match(body);
        if (!hit.Success || !int.TryParse(hit.Groups[1].Value, out var amount))
            return new(time, "unknown", body, "", null);

        var kind = hit.Groups[2].Value is "to" or "на" ? "out" : "in";
        return new(time, kind, hit.Groups[3].Value.TrimEnd('*'), hit.Groups[4].Value, amount);
    }
}
