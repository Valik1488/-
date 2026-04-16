using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using MusicApp.Client;
using MusicApp.Client.Interfaces;
using MusicApp.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

builder.Services.AddOptions();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISpotifyClientService, SpotifyClientService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://192.168.3.6:5238/")
});
var host = builder.Build();

// 🔥 ОЦЕ ГОЛОВНЕ
var authService = host.Services.GetRequiredService<IAuthService>();
await authService.SetAuthHeader();

await host.RunAsync();