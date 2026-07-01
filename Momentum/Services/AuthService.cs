using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Momentum.Data;
using Momentum.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Momentum.Services;

public class AuthService : IAuthService
{
    private readonly MomentumDbContext _dbContext;
    private string? _currentUserEmail;
    private readonly object _lock = new();
    private readonly ProtectedSessionStorage _sessionStorage;
    private const string CurrentUserKey = "currentUserEmail";

    public event Action? OnAuthStateChanged;

    public AuthService(MomentumDbContext dbContext, ProtectedSessionStorage sessionStorage)
    {
        _dbContext = dbContext;
        _sessionStorage = sessionStorage;
        InitializeDefaultUser();
    }

    private async Task<string?> GetOrFetchUserEmailAsync()
    {
        if (_currentUserEmail == null)
        {
            try
            {
                var result = await _sessionStorage.GetAsync<string>(CurrentUserKey);
                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    lock (_lock)
                    {
                        _currentUserEmail = result.Value;
                    }
                }
            }
            catch
            {
                // Session storage access can fail during static rendering/prerendering
            }
        }
        return _currentUserEmail;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var email = await GetOrFetchUserEmailAsync();
        return email != null;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var hash = HashPassword(password);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToUpper() == email.ToUpper());

        if (user != null && user.PasswordHash == hash)
        {
            lock (_lock)
            {
                _currentUserEmail = email;
            }
            try
            {
                await _sessionStorage.SetAsync(CurrentUserKey, email);
            }
            catch
            {
            }
            OnAuthStateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public async Task LogoutAsync()
    {
        lock (_lock)
        {
            _currentUserEmail = null;
        }
        try
        {
            await _sessionStorage.DeleteAsync(CurrentUserKey);
        }
        catch
        {
        }
        OnAuthStateChanged?.Invoke();
    }

    public async Task<string?> GetCurrentUserEmailAsync()
    {
        return await GetOrFetchUserEmailAsync();
    }

    public async Task<string?> GetCurrentUserFirstNameAsync()
    {
        string? email = await GetOrFetchUserEmailAsync();

        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToUpper() == email.ToUpper());
        return user?.FirstName;
    }

    public async Task<bool> RegisterUserAsync(string email, string password, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return false;
        }

        // Check if email already registered in database
        var exists = await _dbContext.Users.AnyAsync(u => u.Email.ToUpper() == email.ToUpper());
        if (exists)
        {
            return false;
        }

        RegisterUser(email, password, firstName, lastName);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetRegisteredUsersAsync()
    {
        return await _dbContext.Users.Select(u => u.Email).ToListAsync();
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        string? email = await GetOrFetchUserEmailAsync();

        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToUpper() == email.ToUpper());
    }

    public async Task<bool> UpdateCurrentUserAsync(string firstName, string lastName, string? newPassword)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return false;
        }

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 8)
            {
                return false;
            }
            user.PasswordHash = HashPassword(newPassword);
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private void InitializeDefaultUser()
    {
        // Check if any users exist in the database; if not, add default user
        if (!_dbContext.Users.Any())
        {
            RegisterUser("john@example.com", "password123", "John", "Doe");
            _dbContext.SaveChanges();
        }
    }

    private void RegisterUser(string email, string password, string firstName, string lastName)
    {
        _dbContext.Users.Add(new User
        {
            Email = email,
            PasswordHash = HashPassword(password),
            FirstName = firstName,
            LastName = lastName
        });
    }

    private string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        StringBuilder builder = new();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }
        return builder.ToString();
    }

    public async Task<bool> ResetPasswordAsync(string email, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToUpper() == email.ToUpper());
        if (user == null)
        {
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
