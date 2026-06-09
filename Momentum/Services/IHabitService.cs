using Momentum.Models;

namespace Momentum.Services;

public interface IHabitService
{
    Task<List<Habit>> GetTodayHabitsAsync();
    Task ToggleHabitCompletionAsync(int habitId);
    Task<List<WeekDay>> GetWeeklyCalendarAsync();
    Task<OverviewStats> GetOverviewStatsAsync();
    Task ResetAllHabitsAsync();
    Task PopulateMockHabitsAsync();
}
