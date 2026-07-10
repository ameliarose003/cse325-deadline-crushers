using Microsoft.EntityFrameworkCore;
using Momentum.Components;
using Momentum.Data;
using Momentum.Services;
using Momentum.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<MomentumDbContext>(options =>
    options.UseSqlite("Data Source=momentum.db"));

builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<ProtectedSessionStorage>();

builder.Services.AddScoped(sp => 
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

var app = builder.Build();

// Ensure database is initialized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MomentumDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/register", async (Momentum.Models.RegisterRequest request, IAuthService authService) =>
{
    if (request == null || 
        string.IsNullOrWhiteSpace(request.Email) || 
        string.IsNullOrWhiteSpace(request.Password) ||
        string.IsNullOrWhiteSpace(request.FirstName) ||
        string.IsNullOrWhiteSpace(request.LastName))
    {
        return Results.BadRequest(new { message = "Email, Password, First Name, and Last Name are required." });
    }

    if (!request.Email.Contains("@"))
    {
        return Results.BadRequest(new { message = "Please enter a valid email address." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { message = "Password must be at least 8 characters." });
    }

    var success = await authService.RegisterUserAsync(request.Email, request.Password, request.FirstName, request.LastName);
    if (success)
    {
        return Results.Ok(new { message = "Registration successful" });
    }

    return Results.BadRequest(new { message = "Email is already registered" });
});

app.MapPost("/api/login", async (Momentum.Models.LoginRequest request, IAuthService authService) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email and Password are required." });
    }

    var success = await authService.LoginAsync(request.Email, request.Password);
    if (success)
    {
        return Results.Ok(new { message = "Login successful", email = request.Email });
    }

    return Results.Json(new { message = "Invalid email or password" }, statusCode: 401);
});

// Habit CRUD Endpoints
app.MapGet("/api/habits", async (int? userId, MomentumDbContext db) =>
{
    var query = db.Habits.AsQueryable();
    if (userId.HasValue)
    {
        query = query.Where(h => h.UserId == userId.Value);
    }
    
    var habits = await query
        .Select(h => new
        {
            h.Id,
            h.Name,
            h.IsCompleted,
            h.Streak,
            h.Category,
            h.CategoryLabel,
            h.UserId
        })
        .ToListAsync();
        
    return Results.Ok(habits);
});

app.MapGet("/api/habits/{id:int}", async (int id, MomentumDbContext db) =>
{
    var habit = await db.Habits
        .Where(h => h.Id == id)
        .Select(h => new
        {
            h.Id,
            h.Name,
            h.IsCompleted,
            h.Streak,
            h.Category,
            h.CategoryLabel,
            h.UserId
        })
        .FirstOrDefaultAsync();

    return habit is not null 
        ? Results.Ok(habit) 
        : Results.NotFound(new { message = $"Habit with ID {id} not found." });
});

app.MapPost("/api/habits", async (Habit habit, MomentumDbContext db) =>
{
    if (habit == null || string.IsNullOrWhiteSpace(habit.Name) || string.IsNullOrWhiteSpace(habit.Category) || string.IsNullOrWhiteSpace(habit.CategoryLabel) || habit.UserId <= 0)
    {
        return Results.BadRequest(new { message = "Name, Category, CategoryLabel, and a valid UserId are required." });
    }

    var userExists = await db.Users.AnyAsync(u => u.Id == habit.UserId);
    if (!userExists)
    {
        return Results.BadRequest(new { message = $"User with ID {habit.UserId} does not exist." });
    }

    // Reset navigation properties to avoid EF issues
    habit.User = null;
    habit.Logs = new List<HabitLog>();

    db.Habits.Add(habit);
    await db.SaveChangesAsync();

    return Results.Created($"/api/habits/{habit.Id}", new
    {
        habit.Id,
        habit.Name,
        habit.IsCompleted,
        habit.Streak,
        habit.Category,
        habit.CategoryLabel,
        habit.UserId
    });
});

app.MapPut("/api/habits/{id:int}", async (int id, Habit inputHabit, MomentumDbContext db) =>
{
    if (inputHabit == null)
    {
        return Results.BadRequest(new { message = "Request body is required." });
    }

    var habit = await db.Habits.FindAsync(id);
    if (habit is null)
    {
        return Results.NotFound(new { message = $"Habit with ID {id} not found." });
    }

    if (string.IsNullOrWhiteSpace(inputHabit.Name) || string.IsNullOrWhiteSpace(inputHabit.Category) || string.IsNullOrWhiteSpace(inputHabit.CategoryLabel))
    {
        return Results.BadRequest(new { message = "Name, Category, and CategoryLabel are required." });
    }

    habit.Name = inputHabit.Name;
    habit.Category = inputHabit.Category;
    habit.CategoryLabel = inputHabit.CategoryLabel;
    habit.IsCompleted = inputHabit.IsCompleted;
    habit.Streak = inputHabit.Streak;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/api/habits/{id:int}", async (int id, MomentumDbContext db) =>
{
    var habit = await db.Habits.FindAsync(id);
    if (habit is null)
    {
        return Results.NotFound(new { message = $"Habit with ID {id} not found." });
    }

    // Remove dependent logs
    var logs = await db.HabitLogs.Where(l => l.HabitId == id).ToListAsync();
    db.HabitLogs.RemoveRange(logs);

    db.Habits.Remove(habit);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = $"Habit with ID {id} and its logs were successfully deleted." });
});

// HabitLog Endpoints
app.MapGet("/api/habits/{habitId:int}/logs", async (int habitId, MomentumDbContext db) =>
{
    var habitExists = await db.Habits.AnyAsync(h => h.Id == habitId);
    if (!habitExists)
    {
        return Results.NotFound(new { message = $"Habit with ID {habitId} not found." });
    }

    var logs = await db.HabitLogs
        .Where(l => l.HabitId == habitId)
        .Select(l => new
        {
            l.Id,
            l.HabitId,
            l.Date,
            l.IsCompleted
        })
        .ToListAsync();

    return Results.Ok(logs);
});

app.MapPost("/api/habits/{habitId:int}/logs", async (int habitId, HabitLog logInput, MomentumDbContext db) =>
{
    var habit = await db.Habits.FindAsync(habitId);
    if (habit is null)
    {
        return Results.NotFound(new { message = $"Habit with ID {habitId} not found." });
    }

    // Default to today if date is not specified
    var date = logInput?.Date ?? DateOnly.FromDateTime(DateTime.Today);

    var existingLog = await db.HabitLogs.FirstOrDefaultAsync(l => l.HabitId == habitId && l.Date == date);
    if (existingLog != null)
    {
        existingLog.IsCompleted = logInput?.IsCompleted ?? true;
    }
    else
    {
        var newLog = new HabitLog
        {
            HabitId = habitId,
            Date = date,
            IsCompleted = logInput?.IsCompleted ?? true
        };
        db.HabitLogs.Add(newLog);
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Habit log status updated successfully." });
});

app.MapDelete("/api/habits/{habitId:int}/logs", async (int habitId, DateOnly? date, MomentumDbContext db) =>
{
    var habitExists = await db.Habits.AnyAsync(h => h.Id == habitId);
    if (!habitExists)
    {
        return Results.NotFound(new { message = $"Habit with ID {habitId} not found." });
    }

    var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
    var existingLog = await db.HabitLogs.FirstOrDefaultAsync(l => l.HabitId == habitId && l.Date == targetDate);
    if (existingLog == null)
    {
        return Results.NotFound(new { message = $"No log found for habit ID {habitId} on date {targetDate}." });
    }

    db.HabitLogs.Remove(existingLog);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = $"Habit log for date {targetDate} deleted successfully." });
});

app.Run();
