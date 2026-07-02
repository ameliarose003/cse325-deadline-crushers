using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Momentum.Services;

public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public CustomAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) 
        : base(options, logger, encoder) 
    { 
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Return a dummy authenticated identity with a Name claim to satisfy Kestrel's HTTP pipeline and Antiforgery requirements.
        // Blazor's CustomAuthenticationStateProvider will handle the actual user authentication inside the circuit.
        var claims = new[] { new Claim(ClaimTypes.Name, "dummy") };
        var identity = new ClaimsIdentity(claims, "CustomIdentity");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
