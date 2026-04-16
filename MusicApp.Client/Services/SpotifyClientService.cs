using Microsoft.JSInterop;
using MusicApp.Client.Interfaces;
using MusicApp.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;

namespace MusicApp.Client.Services;

public class SpotifyClientService : ISpotifyClientService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly IJSRuntime _jsRuntime;

    public SpotifyClientService(
        HttpClient httpClient,
        IAuthService authService,
        IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _authService = authService;
        _jsRuntime = jsRuntime;
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequest(HttpMethod method, string url)
    {
        var token = await _authService.GetToken();

        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException("No auth token found");

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("401 Unauthorized");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<T>();

        if (result == null)
            throw new InvalidOperationException("Failed to deserialize response");

        return result;
    }

    public async Task<SpotifyUserProfile> GetUserProfileAsync()
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Get, "api/spotify/profile");
        return await SendAsync<SpotifyUserProfile>(request);
    }

    public async Task<List<SpotifyPlaylistDto>> GetUserPlaylistsAsync()
    {
        var request = await CreateAuthorizedRequest(HttpMethod.Get, "api/spotify/playlists");
        return await SendAsync<List<SpotifyPlaylistDto>>(request);
    }

    public async Task<SpotifyPlaylistDto> GetPlaylistByIdAsync(string playlistId)
    {
        var playlistJson = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "current_playlist");

        if (!string.IsNullOrEmpty(playlistJson))
        {
            var cached = JsonSerializer.Deserialize<SpotifyPlaylistDto>(playlistJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cached?.Id == playlistId)
                return cached;
        }

        try
        {
            var request = await CreateAuthorizedRequest(HttpMethod.Get, $"api/spotify/playlists/{playlistId}");
            var playlist = await SendAsync<SpotifyPlaylistDto>(request);

            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "current_playlist",
                JsonSerializer.Serialize(playlist));

            return playlist;
        }
        catch
        {
            var playlists = await GetUserPlaylistsAsync();
            var playlist = playlists.FirstOrDefault(p => p.Id == playlistId);

            if (playlist != null)
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "current_playlist",
                    JsonSerializer.Serialize(playlist));

                return playlist;
            }

            throw new InvalidOperationException($"Playlist {playlistId} not found");
        }
    }

    public async Task<List<SpotifyTrackDto>> GetPlaylistRecommendationsAsync(string playlistId)
    {
        var useCache = await ShouldUseCachedRecommendationsAsync(playlistId);

        if (useCache)
        {
            var cached = await LoadCachedRecommendationsAsync(playlistId);

            if (cached != null && cached.Count > 0 &&
                !cached.Any(t => t.Album?.Images == null || !t.Album.Images.Any()))
            {
                return cached;
            }
        }

        var request = await CreateAuthorizedRequest(HttpMethod.Get,
            $"api/spotify/playlists/{playlistId}/recommendations");

        var recommendations = await SendAsync<List<SpotifyTrackDto>>(request);

        if (recommendations.Count > 0)
            await CacheRecommendationsAsync(playlistId, recommendations);

        return recommendations;
    }

    public async Task<List<SpotifyArtistDto>> SearchArtistsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SpotifyArtistDto>();

        var cacheKey = $"artist_search_{query.ToLower()}";
        var cached = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", cacheKey);

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var data = JsonSerializer.Deserialize<List<SpotifyArtistDto>>(cached,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null && data.Count > 0)
                    return data;
            }
            catch { }
        }

        var encoded = Uri.EscapeDataString(query);

        var request = await CreateAuthorizedRequest(HttpMethod.Get,
            $"api/spotify/search/artists?query={encoded}");

        var artists = await SendAsync<List<SpotifyArtistDto>>(request);

        if (artists.Count > 0)
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem",
                cacheKey,
                JsonSerializer.Serialize(artists));
        }

        return artists;
    }

    public async Task<List<dynamic>> GetRelatedArtistsAsync(string artistId)
    {
        if (string.IsNullOrWhiteSpace(artistId))
            return new List<dynamic>();

        var cacheKey = $"related_artists_{artistId}";
        var cached = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", cacheKey);

        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var data = JsonSerializer.Deserialize<List<dynamic>>(cached,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null && data.Count > 0)
                    return data;
            }
            catch { }
        }

        var request = await CreateAuthorizedRequest(HttpMethod.Get,
            $"api/spotify/artists/{artistId}/related");

        var related = await SendAsync<List<dynamic>>(request);

        if (related.Count > 0)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem",
                cacheKey,
                JsonSerializer.Serialize(related));
        }

        return related;
    }

    // 🔽 кеш залишив як є

    private async Task<bool> ShouldUseCachedRecommendationsAsync(string playlistId)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem",
                $"spotify_recommendations_timestamp_{playlistId}");

            if (string.IsNullOrEmpty(json)) return false;

            var time = JsonSerializer.Deserialize<DateTime>(json);

            return (DateTime.Now - time).TotalHours < 24;
        }
        catch { return false; }
    }

    private async Task<List<SpotifyTrackDto>?> LoadCachedRecommendationsAsync(string playlistId)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem",
                $"spotify_recommendations_{playlistId}");

            if (string.IsNullOrEmpty(json)) return null;

            return JsonSerializer.Deserialize<List<SpotifyTrackDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private async Task CacheRecommendationsAsync(string playlistId, List<SpotifyTrackDto> recommendations)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem",
                $"spotify_recommendations_{playlistId}",
                JsonSerializer.Serialize(recommendations));

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem",
                $"spotify_recommendations_timestamp_{playlistId}",
                JsonSerializer.Serialize(DateTime.Now));
        }
        catch { }
    }

    public async Task ClearStoredRecommendations(string playlistId)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem",
            $"spotify_recommendations_{playlistId}");

        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem",
            $"spotify_recommendations_timestamp_{playlistId}");
    }

    public async Task<bool> HasStoredRecommendations(string playlistId)
    {
        var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem",
            $"spotify_recommendations_{playlistId}");

        return !string.IsNullOrEmpty(json);
    }
}