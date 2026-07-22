using System.Globalization;
using Microsoft.Extensions.ObjectPool;
using Momentum.Models;
using Microsoft.EntityFrameworkCore;
using Momentum.Data;
using Momentum.Components.Pages;


namespace Momentum.Services;

public class HabitService : IHabitService
{
    private readonly List<Habit> _habits = new();
    private readonly List<WeekDay> _weekDays = new();
    private readonly object _lock = new();
    private readonly MomentumDbContext _dbContext;
    private readonly IAuthService _authService;

    public HabitService(
        MomentumDbContext dbContext,
        IAuthService authService)
        {
            _dbContext = dbContext;
            _authService = authService;

            InitializeCalendar();
        }

    public async Task<List<Habit>> GetTodayHabitsAsync()
    {
        var habits = await GetUserHabitsWithLogsAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var habit in habits)
        {
            habit.IsCompleted = habit.Logs.Any(log => log.Date == today && log.IsCompleted);
        }
        
        lock (_lock)
        {
            _habits.Clear();
            _habits.AddRange(habits);
            UpdateCalendarState();
        }

        return habits;
    }

    public async Task ToggleHabitCompletionAsync(int habitId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            throw new InvalidOperationException("User must log in");
        }
        var habit = await _dbContext.Habits.Include(h => h.Logs).FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == currentUser.Id);
        if (habit == null)
        {
            return;
        }
        var existingLog = habit.Logs.FirstOrDefault(log => log.Date == today && log.IsCompleted);
        if (existingLog != null)
        {
            _dbContext.HabitLogs.Remove(existingLog);
            habit.Logs.Remove(existingLog);
        }
        else
        {
            var newLog = new HabitLog
            {
                HabitId = habit.Id,
                Date = today,
                IsCompleted = true
            };

            _dbContext.HabitLogs.Add(newLog);
            habit.Logs.Add(newLog);
        }
        habit.IsCompleted = existingLog == null;
        habit.Streak = CalculateDatabaseStreak(habit);
        habit.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync();
    }
    private static int CalculateDatabaseStreak(Habit habit)
{
    var today = DateOnly.FromDateTime(DateTime.Today);

    var completedDates = habit.Logs
        .Where(log => log.IsCompleted)
        .Select(log => log.Date)
        .ToHashSet();

    var startDate = completedDates.Contains(today)
        ? today
        : today.AddDays(-1);

    var streak = 0;
    var checkDate = startDate;

    while (completedDates.Contains(checkDate))
    {
        streak++;
        checkDate = checkDate.AddDays(-1);
    }

    return streak;
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

    public async Task AddNewHabitAsync(string name, string category)
    {
        await PopulateNewHabits(name, category);
    }


    private async Task PopulateNewHabits(string Name, string Category)
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            throw new InvalidOperationException ("User must be logged in");
        }
        
        var habit = new Habit  {UserId = currentUser.Id, Name = Name, IsCompleted = false, Streak = 0, Category = Category, CategoryLabel = Category.ToUpper()};

        _dbContext.Habits.Add(habit);
        await _dbContext.SaveChangesAsync();

        lock (_lock)
        {
        _habits.Add(habit);
        UpdateCalendarState();
        }

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
    public async Task<List<Habit>> GetUserHabitsWithLogsAsync()
    {
        var currentUser = await _authService.GetCurrentUserAsync();

        if (currentUser == null)
        {
            return new List<Habit>();
        }
        return await _dbContext.Habits
        .AsNoTracking()
        .Where(habit => habit.UserId == currentUser.Id)
        .Include(habit => habit.Logs)
        .OrderBy(habit => habit.Id)
        .ToListAsync();
    }
}