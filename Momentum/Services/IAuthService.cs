using Momentum.Models;

namespace Momentum.Services;

public interface IAuthService
{
    Task<bool> IsAuthenticatedAsync();
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<string?> GetCurrentUserEmailAsync();
    Task<string?> GetCurrentUserFirstNameAsync();
    Task<bool> RegisterUserAsync(string email, string password, string firstName, string lastName);
    Task<List<string>> GetRegisteredUsersAsync();
    Task<User?> GetCurrentUserAsync();
    Task<bool> UpdateCurrentUserAsync(string firstName, string lastName, string? newPassword);
}
