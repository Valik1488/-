using Microsoft.AspNetCore.Components.Authorization;
using MusicApp.Client.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;

namespace MusicApp.Client;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _httpClient;

    public CustomAuthStateProvider(
        ILocalStorageService localStorage,
        HttpClient httpClient)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // 🔥 ГАРАНТУЄМО що header є
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // 🔥 ВАЖЛИВО: authType має бути НЕ null
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");

            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}