namespace Momentum.Models;

public class WeekDay
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsToday { get; set; }
}
