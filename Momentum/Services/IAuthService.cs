namespace Momentum.Services;

public interface IAuthService
{
    Task<bool> IsAuthenticatedAsync();
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<string?> GetCurrentUserEmailAsync();
    Task<bool> RegisterUserAsync(string email, string password);
    Task<List<string>> GetRegisteredUsersAsync();
}
