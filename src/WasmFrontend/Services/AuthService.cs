using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using WasmFrontend.Models;

namespace WasmFrontend.Services;

public sealed class AuthService
{
    private const string TokenKey = "auth_token";
    private const string UserIdKey = "user_id";
    private const string UserEmailKey = "user_email";
    private const string UserNameKey = "user_name";
    private const string UserRoleKey = "user_role";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? UserName { get; private set; }
    public string? UserRole { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public event Action? AuthStateChanged;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        Token = await _js.InvokeAsync<string>("localStorage.getItem", TokenKey);
        UserId = await _js.InvokeAsync<string>("localStorage.getItem", UserIdKey);
        UserEmail = await _js.InvokeAsync<string>("localStorage.getItem", UserEmailKey);
        UserName = await _js.InvokeAsync<string>("localStorage.getItem", UserNameKey);
        UserRole = await _js.InvokeAsync<string>("localStorage.getItem", UserRoleKey);

        if (!string.IsNullOrEmpty(Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            try
            {
                var validateResponse = await _http.GetAsync("/api/auth/validate");
                if (!validateResponse.IsSuccessStatusCode)
                    await LogoutAsync();
            }
            catch
            {
                await LogoutAsync();
            }
        }
    }

    public async Task<(AuthResponse? Result, string? Error)> Login(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                return (null, body?.GetValueOrDefault("error", "Invalid email or password") ?? "Invalid email or password");
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result is null)
                return (null, "Invalid server response");

            await StoreAuthAsync(result);
            return (result, null);
        }
        catch (Exception ex)
        {
            return (null, $"Connection error: {ex.Message}");
        }
    }

    public async Task<string?> Register(string email, string password, string name)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, name));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return error?.GetValueOrDefault("error", "Registration failed");
        }
        return null;
    }

    public async Task LogoutAsync()
    {
        Token = null;
        UserId = null;
        UserEmail = null;
        UserName = null;
        UserRole = null;
        _http.DefaultRequestHeaders.Authorization = null;

        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", UserEmailKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", UserNameKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", UserRoleKey);

        AuthStateChanged?.Invoke();
    }

    private async Task StoreAuthAsync(AuthResponse auth)
    {
        Token = auth.Token;
        UserId = auth.UserId;
        UserEmail = auth.Email;
        UserName = auth.Name;
        UserRole = auth.Role;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, auth.Token);
        await _js.InvokeVoidAsync("localStorage.setItem", UserIdKey, auth.UserId);
        await _js.InvokeVoidAsync("localStorage.setItem", UserEmailKey, auth.Email);
        await _js.InvokeVoidAsync("localStorage.setItem", UserNameKey, auth.Name);
        await _js.InvokeVoidAsync("localStorage.setItem", UserRoleKey, auth.Role);

        AuthStateChanged?.Invoke();
    }
}
