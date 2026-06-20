using Microsoft.EntityFrameworkCore;
using Momentum.Components;
using Momentum.Data;
using Momentum.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<MomentumDbContext>(options =>
    options.UseSqlite("Data Source=momentum.db"));

builder.Services.AddSingleton<IHabitService, HabitService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<ProtectedSessionStorage>();

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

app.Run();
