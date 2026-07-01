using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Momentum.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthService _authService;

    public CustomAuthenticationStateProvider(IAuthService authService)
    {
        _authService = authService;
        _authService.OnAuthStateChanged += HandleAuthStateChanged;
    }

    private void HandleAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var email = await _authService.GetCurrentUserEmailAsync();
        
        if (string.IsNullOrEmpty(email))
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(anonymous);
        }

        var claims = new[] { new Claim(ClaimTypes.Name, email) };
        var identity = new ClaimsIdentity(claims, "CustomAuth");
        var user = new ClaimsPrincipal(identity);
        
        return new AuthenticationState(user);
    }

    public void Dispose()
    {
        _authService.OnAuthStateChanged -= HandleAuthStateChanged;
    }
}
