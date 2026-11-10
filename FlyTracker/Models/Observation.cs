namespace FlyTracker.Models;

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
