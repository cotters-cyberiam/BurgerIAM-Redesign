using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace WasmFrontend.Services;

public sealed class BurgerIAMAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _auth;

    public BurgerIAMAuthStateProvider(AuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += NotifyAuthStateChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_auth.IsLoggedIn || _auth.UserId is null)
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonymous));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _auth.UserId),
            new Claim(ClaimTypes.Email, _auth.UserEmail ?? ""),
            new Claim(ClaimTypes.Name, _auth.UserName ?? ""),
            new Claim(ClaimTypes.Role, _auth.UserRole ?? "Customer")
        };

        var identity = new ClaimsIdentity(claims, "BurgerIAM");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }

    private void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
