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
    Task<bool> ResetPasswordAsync(string email, string newPassword);
}
