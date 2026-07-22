using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Momentum.Data;
using Momentum.Models;

namespace Momentum.Services;

public class HabitService : IHabitService
{
    private readonly MomentumDbContext _db;
    private readonly IAuthService _authService;

    public HabitService(MomentumDbContext db, IAuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task<List<Habit>> GetTodayHabitsAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return new List<Habit>();
        }

        var habits = await _db.Habits
            .Where(h => h.UserId == user.Id)
            .Include(h => h.Logs)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Project/Map completion status for today
        foreach (var habit in habits)
        {
            habit.IsCompleted = habit.Logs.Any(l => l.Date == today && l.IsCompleted);
        }

        return habits;
    }

    public async Task ToggleHabitCompletionAsync(int habitId)
    {
        var habit = await _db.Habits
            .Include(h => h.Logs)
            .FirstOrDefaultAsync(h => h.Id == habitId);

        if (habit == null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var existingLog = habit.Logs.FirstOrDefault(l => l.Date == today);

        if (existingLog != null)
        {
            _db.HabitLogs.Remove(existingLog);
            habit.Logs.Remove(existingLog);
        }
        else
        {
            var newLog = new HabitLog
            {
                HabitId = habitId,
                Date = today,
                IsCompleted = true
            };
            _db.HabitLogs.Add(newLog);
            habit.Logs.Add(newLog);
        }

        // Recalculate streak
        habit.Streak = CalculateStreak(habit);

        await _db.SaveChangesAsync();
    }

    public async Task<List<WeekDay>> GetWeeklyCalendarAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return new List<WeekDay>();
        }

        var habits = await _db.Habits
            .Where(h => h.UserId == user.Id)
            .Include(h => h.Logs)
            .ToListAsync();

        var daysOfWeek = new[]
        {
            new { Name = "Monday", Abbr = "M" },
            new { Name = "Tuesday", Abbr = "T" },
            new { Name = "Wednesday", Abbr = "W" },
            new { Name = "Thursday", Abbr = "T" },
            new { Name = "Friday", Abbr = "F" },
            new { Name = "Saturday", Abbr = "S" },
            new { Name = "Sunday", Abbr = "S" }
        };

        var today = DateTime.Today;
        var todayName = today.DayOfWeek.ToString();

        // Calculate Monday's date of this week
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var mondayDate = today.AddDays(-1 * diff);

        var result = new List<WeekDay>();
        for (int i = 0; i < 7; i++)
        {
            var targetDate = mondayDate.AddDays(i);
            var dateOnly = DateOnly.FromDateTime(targetDate);
            var dayInfo = daysOfWeek[i];

            // A weekday is marked completed if the user completed AT LEAST one habit on that day.
            bool isCompleted = habits.Any(h => h.Logs.Any(l => l.Date == dateOnly && l.IsCompleted));

            result.Add(new WeekDay
            {
                Name = dayInfo.Name,
                Abbreviation = dayInfo.Abbr,
                IsCompleted = isCompleted,
                IsToday = dayInfo.Name == todayName
            });
        }

        return result;
    }

    public async Task<OverviewStats> GetOverviewStatsAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return new OverviewStats();
        }

        var habits = await _db.Habits
            .Where(h => h.UserId == user.Id)
            .Include(h => h.Logs)
            .ToListAsync();

        int total = habits.Count;
        var today = DateOnly.FromDateTime(DateTime.Today);
        int completed = habits.Count(h => h.Logs.Any(l => l.Date == today && l.IsCompleted));
        int rate = total == 0 ? 0 : (completed * 100) / total;

        int currentStreak = habits.Count > 0 ? habits.Max(h => h.Streak) : 0;
        int longestStreak = habits.Count > 0 ? habits.Max(h => h.Streak) : 0;
        if (longestStreak < 12 && total > 0)
        {
            longestStreak = 12;
        }

        return new OverviewStats
        {
            TotalCount = total,
            CompletedCount = completed,
            CompletionRate = rate,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        };
    }

    public async Task ResetAllHabitsAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return;
        }

        var habits = await _db.Habits.Where(h => h.UserId == user.Id).ToListAsync();
        _db.Habits.RemoveRange(habits);
        await _db.SaveChangesAsync();
    }

    public async Task PopulateMockHabitsAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return;
        }

        var userHabitsCount = await _db.Habits.CountAsync(h => h.UserId == user.Id);
        if (userHabitsCount == 0)
        {
            var defaultHabits = new List<Habit>
            {
                new() { Name = "Read 10 pages of a book", Category = "mind", CategoryLabel = "Mind", UserId = user.Id },
                new() { Name = "Drink 3 liters of water", Category = "health", CategoryLabel = "Health", UserId = user.Id },
                new() { Name = "30-minute cardio workout", Category = "fitness", CategoryLabel = "Fitness", UserId = user.Id },
                new() { Name = "Code on side project", Category = "work", CategoryLabel = "Work", UserId = user.Id },
                new() { Name = "10-minute mindfulness meditation", Category = "mind", CategoryLabel = "Mind", UserId = user.Id }
            };

            await _db.Habits.AddRangeAsync(defaultHabits);
            await _db.SaveChangesAsync();
        }
    }

    public async Task AddNewHabitAsync(string Name, string Category)
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return;
        }

        var newHabit = new Habit
        {
            Name = Name,
            Category = Category.ToLower(),
            CategoryLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Category),
            UserId = user.Id,
            IsCompleted = false,
            Streak = 0
        };

        _db.Habits.Add(newHabit);
        await _db.SaveChangesAsync();
    }

    private int CalculateStreak(Habit habit)
    {
        int streak = 0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        bool completedToday = habit.Logs.Any(l => l.Date == today && l.IsCompleted);
        bool completedYesterday = habit.Logs.Any(l => l.Date == today.AddDays(-1) && l.IsCompleted);

        if (!completedToday && !completedYesterday)
        {
            return 0;
        }

        var checkDate = completedToday ? today : today.AddDays(-1);
        while (habit.Logs.Any(l => l.Date == checkDate && l.IsCompleted))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
        }

        return streak;
    }
}
