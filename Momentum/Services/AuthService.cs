using System.Security.Cryptography;
using System.Text;
using Momentum.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Momentum.Services;

public class AuthService : IAuthService
{
    private string? _currentUserEmail;
    private readonly List<User> _users = new();
    private readonly object _lock = new();
    private readonly ProtectedSessionStorage _sessionStorage;
    private const string CurrentUserKey = "currentUserEmail";

    public AuthService(ProtectedSessionStorage sessionStorage)
    {
        // Pre-populate a default user for testing purposes
        _sessionStorage = sessionStorage;
        RegisterUser("john@example.com", "password123");
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_currentUserEmail != null);
        }
    }

    public Task<bool> LoginAsync(string email, string password)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Task.FromResult(false);
            }

            var hash = HashPassword(password);
            var user = _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            
            if (user != null && user.PasswordHash == hash)
            {
                _currentUserEmail = email;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public Task LogoutAsync()
    {
        lock (_lock)
        {
            _currentUserEmail = null;
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetCurrentUserEmailAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_currentUserEmail);
        }
    }

    public Task<bool> RegisterUserAsync(string email, string password)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Task.FromResult(false);
            }

            // Check if email already registered
            var exists = _users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return Task.FromResult(false);
            }

            RegisterUser(email, password);
            return Task.FromResult(true);
        }
    }

    public Task<List<string>> GetRegisteredUsersAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_users.Select(u => u.Email).ToList());
        }
    }

    private void RegisterUser(string email, string password)
    {
        _users.Add(new User
        {
            Email = email,
            PasswordHash = HashPassword(password)
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
}
