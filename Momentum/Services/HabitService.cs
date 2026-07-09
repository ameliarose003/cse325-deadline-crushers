using System.Globalization;
using Microsoft.Extensions.ObjectPool;
using Momentum.Models;

namespace Momentum.Services;

public class HabitService : IHabitService
{
    private readonly List<Habit> _habits = new();
    private readonly List<WeekDay> _weekDays = new();
    private readonly object _lock = new();

    public HabitService()
    {
        InitializeCalendar();
        PopulateDefaultHabits();
        ResetDailyHabits();
    }

    public Task<List<Habit>> GetTodayHabitsAsync()
    {
        lock (_lock)
        {
            // Return a copy of the list to prevent external modification issues
            return Task.FromResult(_habits.Select(h => new Habit
            {
                Id = h.Id,
                Name = h.Name,
                IsCompleted = h.IsCompleted,
                Streak = h.Streak,
                Category = h.Category,
                CategoryLabel = h.CategoryLabel,
                UpdatedAt = DateTime.Now.Date
            }).ToList());
        }
    }

    public Task ToggleHabitCompletionAsync(int habitId)
    {
        lock (_lock)
        {
            var habit = _habits.FirstOrDefault(h => h.Id == habitId);
            if (habit != null)
            {
                habit.IsCompleted = !habit.IsCompleted;
                if (habit.IsCompleted)
                {
                    habit.Streak++;
                    habit.UpdatedAt = DateTime.Now.Date;
                }
                else
                {
                    habit.Streak--;
                    habit.UpdatedAt = null;
                }

                UpdateCalendarState();
            }
        }
        return Task.CompletedTask;
    }

    public void ResetDailyHabits()
    {
        foreach (var habit in _habits)
        {
            if (habit.UpdatedAt != DateTime.Now.Date)
            {
                habit.IsCompleted = false;
            }
            
        }
    }

    public Task<List<WeekDay>> GetWeeklyCalendarAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_weekDays.Select(d => new WeekDay
            {
                Name = d.Name,
                Abbreviation = d.Abbreviation,
                IsCompleted = d.IsCompleted,
                IsToday = d.IsToday
            }).ToList());
        }
    }

    public Task<OverviewStats> GetOverviewStatsAsync()
    {
        lock (_lock)
        {
            int total = _habits.Count;
            int completed = _habits.Count(h => h.IsCompleted);
            int rate = total == 0 ? 0 : (completed * 100) / total;
            int streak = completed > 0 ? 5 : 4;

            var stats = new OverviewStats
            {
                TotalCount = total,
                CompletedCount = completed,
                CompletionRate = rate,
                CurrentStreak = streak,
                LongestStreak = 12
            };
            return Task.FromResult(stats);
        }
    }

    public Task ResetAllHabitsAsync()
    {
        lock (_lock)
        {
            _habits.Clear();
            UpdateCalendarState();
        }
        return Task.CompletedTask;
    }

    public Task PopulateMockHabitsAsync()
    {
        lock (_lock)
        {
            PopulateDefaultHabits();
        }
        return Task.CompletedTask;
    }

    public Task AddNewHabitAsync(string Name, string Category)
    {
        lock (_lock)
        {
            PopulateNewHabits(Name, Category);
        }
        return Task.CompletedTask;

    }

    private void PopulateNewHabits(string Name, string Category)
    {
        int newId = _habits.Count() == 0 ? 1 : _habits.Max(h => h.Id) + 1;
        _habits.Add(new()  {Id = newId, Name = Name, IsCompleted = false, Streak = 0, Category = Category, CategoryLabel = Category.ToUpper()});
        UpdateCalendarState();

    }

    private void PopulateDefaultHabits()
    {
        _habits.Clear();
        _habits.AddRange(new List<Habit>
        {
            new() { Id = 1, Name = "Read 10 pages of a book", IsCompleted = true, Streak = 5, Category = "mind", CategoryLabel = "Mind", UpdatedAt = DateTime.Now.Date.AddDays(-1) },
            new() { Id = 2, Name = "Drink 3 liters of water", IsCompleted = false, Streak = 2, Category = "health", CategoryLabel = "Health" },
            new() { Id = 3, Name = "30-minute cardio workout", IsCompleted = true, Streak = 9, Category = "fitness", CategoryLabel = "Fitness" },
            new() { Id = 4, Name = "Code on side project", IsCompleted = false, Streak = 4, Category = "work", CategoryLabel = "Work" },
            new() { Id = 5, Name = "10-minute mindfulness meditation", IsCompleted = true, Streak = 1, Category = "mind", CategoryLabel = "Mind" }
        });
        UpdateCalendarState();
    }

    private void InitializeCalendar()
    {
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

        var todayName = DateTime.Now.DayOfWeek.ToString();

        _weekDays.Clear();
        _weekDays.AddRange(daysOfWeek.Select(d => new WeekDay
        {
            Name = d.Name,
            Abbreviation = d.Abbr,
            IsCompleted = d.Name == "Monday" || d.Name == "Wednesday",
            IsToday = d.Name == todayName
        }).ToList());
        
    }

    private void UpdateCalendarState()
    {
        var todayName = DateTime.Now.DayOfWeek.ToString();
        var today = _weekDays.FirstOrDefault(d => d.Name == todayName);
        if (today != null)
        {
            int completed = _habits.Count(h => h.IsCompleted);
            today.IsCompleted = completed > 0;
        }
    }
}
