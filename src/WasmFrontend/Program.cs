using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WasmFrontend;
using WasmFrontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<AuthenticationStateProvider, BurgerIAMAuthStateProvider>();
builder.Services.AddAuthorizationCore();

var host = builder.Build();

var auth = host.Services.GetRequiredService<AuthService>();
await auth.InitializeAsync();

await host.RunAsync();
