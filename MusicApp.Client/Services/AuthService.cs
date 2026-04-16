using Microsoft.JSInterop;
using MusicApp.Client.Interfaces;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using MusicApp.Shared.Models;

namespace MusicApp.Client.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<string> GetSpotifyLoginUrlAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<SpotifyLoginUrlResponse>("api/auth/spotify-login");
        return response!.Url ?? throw new InvalidOperationException("Failed to retrieve Spotify login URL.");
    }

    public async Task<bool> HandleSpotifyCallbackAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        await _localStorage.SetItemAsync("authToken", token);

        // 🔥 ДОДАНО: встановлення токена в HttpClient
        await SetAuthHeader();

        if (_authStateProvider is CustomAuthStateProvider customProvider)
        {
            customProvider.NotifyAuthenticationStateChanged();
        }
        else
        {
            Console.WriteLine("Warning: Using default auth state provider, manual state refresh might be needed");
        }

        return true;
    }

    public async Task<bool> IsUserAuthenticated()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity?.IsAuthenticated ?? false;
    }

    public async Task<string> GetToken()
    {
        return await _localStorage.GetItemAsync<string>("authToken");
    }

    // 🔥 НОВИЙ МЕТОД
    public async Task SetAuthHeader()
    {
        var token = await GetToken();

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("authToken");

        // 🔥 очищаємо заголовок
        _httpClient.DefaultRequestHeaders.Authorization = null;

        if (_authStateProvider is CustomAuthStateProvider customProvider)
        {
            customProvider.NotifyAuthenticationStateChanged();
        }
        else
        {
            Console.WriteLine("Warning: Using default auth state provider, manual state refresh might be needed");
        }
    }
}