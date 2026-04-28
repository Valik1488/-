using Microsoft.JSInterop;
using MusicApp.Client.Interfaces;
using MusicApp.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task<SpotifyUserProfile> GetUserProfileAsync()
    {
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        return await _httpClient.GetFromJsonAsync<SpotifyUserProfile>("api/spotify/profile") ?? throw new InvalidOperationException("Failed to retrieve user profile.");
    }

    public async Task<List<SpotifyPlaylistDto>> GetUserPlaylistsAsync()
    {
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        return await _httpClient.GetFromJsonAsync<List<SpotifyPlaylistDto>>("api/spotify/playlists") ?? throw new InvalidOperationException("Failed to retrieve user playlists.");
    }

    public async Task<SpotifyPlaylistDto> GetPlaylistByIdAsync(string playlistId)
    {
        // First try to get from session storage
        var playlistJson = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "current_playlist");
        if (!string.IsNullOrEmpty(playlistJson))
        {
            var cachedPlaylist = JsonSerializer.Deserialize<SpotifyPlaylistDto>(playlistJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cachedPlaylist?.Id == playlistId)
            {
                return cachedPlaylist;
            }
        }
        
        // If not in session storage, get from API
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        // Note: This assumes you have an API endpoint for getting a single playlist
        // If not, you'd need to modify the playlists controller to add this endpoint
        try {
            var playlist = await _httpClient.GetFromJsonAsync<SpotifyPlaylistDto>($"api/spotify/playlists/{playlistId}");
            
            // Store in session storage
            if (playlist != null) {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "current_playlist", 
                    JsonSerializer.Serialize(playlist));
            }
            
            return playlist ?? throw new InvalidOperationException($"Failed to retrieve playlist with ID {playlistId}.");
        }
        catch {
            // If the specific endpoint doesn't exist, fall back to getting all playlists and finding the one we want
            var playlists = await GetUserPlaylistsAsync();
            var playlist = playlists.FirstOrDefault(p => p.Id == playlistId);
            
            if (playlist != null) {
                // Store in session storage
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "current_playlist", 
                    JsonSerializer.Serialize(playlist));
                return playlist;
            }
            
            throw new InvalidOperationException($"Failed to retrieve playlist with ID {playlistId}.");
        }
    }

    public async Task<List<SpotifyTrackDto>> GetPlaylistRecommendationsAsync(string playlistId)
    {
        // Check if we should use cached recommendations
        var useCache = await ShouldUseCachedRecommendationsAsync(playlistId);
        if (useCache)
        {
            var cachedRecommendations = await LoadCachedRecommendationsAsync(playlistId);
            if (cachedRecommendations != null && cachedRecommendations.Count > 0)
            {
                // Verify all tracks have album images
                if (!cachedRecommendations.Any(t => t.Album?.Images == null || !t.Album.Images.Any()))
                {
                    return cachedRecommendations;
                }
            }
        }

        // Get fresh recommendations from API
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var recommendations = await _httpClient.GetFromJsonAsync<List<SpotifyTrackDto>>($"api/spotify/playlists/{playlistId}/recommendations") ?? 
            new List<SpotifyTrackDto>();
        
        // Store the new recommendations in localStorage
        if (recommendations.Count > 0)
        {
            await CacheRecommendationsAsync(playlistId, recommendations);
        }
        
        return recommendations;
    }
    
    private async Task<bool> ShouldUseCachedRecommendationsAsync(string playlistId)
    {
        try
        {
            // Check if we have a timestamp for when recommendations were last fetched
            var timestampJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", $"spotify_recommendations_timestamp_{playlistId}");
            if (string.IsNullOrEmpty(timestampJson))
            {
                return false;
            }
            
            var timestamp = JsonSerializer.Deserialize<DateTime>(timestampJson);
            
            // Use cached recommendations if they're less than 24 hours old
            return (DateTime.Now - timestamp).TotalHours < 24;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<List<SpotifyTrackDto>?> LoadCachedRecommendationsAsync(string playlistId)
    {
        try
        {
            var storedRecommendationsJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", $"spotify_recommendations_{playlistId}");
            if (string.IsNullOrEmpty(storedRecommendationsJson))
            {
                return null;
            }
            
            return JsonSerializer.Deserialize<List<SpotifyTrackDto>>(storedRecommendationsJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
    
    private async Task CacheRecommendationsAsync(string playlistId, List<SpotifyTrackDto> recommendations)
    {
        try
        {
            // Ensure all tracks have the required properties, especially album images
            foreach (var track in recommendations)
            {
                // Ensure album exists
                if (track.Album == null)
                {
                    track.Album = new SpotifyAlbumDto { Name = "Unknown Album" };
                }
                
                // Ensure album has images
                if (track.Album.Images == null || !track.Album.Images.Any())
                {
                    track.Album.Images = new List<SpotifyImage> 
                    { 
                        new SpotifyImage { Url = "/images/default-album.png" } 
                    };
                }
                
                // Ensure artists exists
                if (track.Artists == null || !track.Artists.Any())
                {
                    track.Artists = new List<SpotifyArtistDto> 
                    { 
                        new SpotifyArtistDto { Name = "Unknown Artist" } 
                    };
                }
            }
            
            // Store recommendations
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                $"spotify_recommendations_{playlistId}", 
                JsonSerializer.Serialize(recommendations));
            
            // Store timestamp
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                $"spotify_recommendations_timestamp_{playlistId}", 
                JsonSerializer.Serialize(DateTime.Now));
        }
        catch
        {
            // Log error or handle silently - caching failure shouldn't break the app
        }
    }
    
    public async Task ClearStoredRecommendations(string playlistId)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"spotify_recommendations_{playlistId}");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"spotify_recommendations_timestamp_{playlistId}");
    }
    
    public async Task<bool> HasStoredRecommendations(string playlistId)
    {
        var storedRecommendationsJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", $"spotify_recommendations_{playlistId}");
        return !string.IsNullOrEmpty(storedRecommendationsJson);
    }

    public async Task<List<SpotifyArtistDto>> SearchArtistsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SpotifyArtistDto>();
        
        // Check if we have cached results
        var cacheKey = $"artist_search_{query.ToLower()}";
        var cachedResults = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", cacheKey);
        
        if (!string.IsNullOrEmpty(cachedResults))
        {
            try
            {
                var cachedArtists = JsonSerializer.Deserialize<List<SpotifyArtistDto>>(cachedResults,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                if (cachedArtists != null && cachedArtists.Count > 0)
                    return cachedArtists;
            }
            catch
            {
                // Failed to deserialize, continue to fetch fresh data
            }
        }
        
        // Get token and set authorization header
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        // Call the API endpoint
        var encodedQuery = Uri.EscapeDataString(query);
        var artists = await _httpClient.GetFromJsonAsync<List<SpotifyArtistDto>>($"api/spotify/search/artists?query={encodedQuery}")
            ?? new List<SpotifyArtistDto>();
        
        // Cache the results
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
        
        // Check if we have cached results
        var cacheKey = $"related_artists_{artistId}";
        var cachedResults = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", cacheKey);
        
        if (!string.IsNullOrEmpty(cachedResults))
        {
            try
            {
                var cachedArtists = JsonSerializer.Deserialize<List<dynamic>>(cachedResults,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                if (cachedArtists != null && cachedArtists.Count > 0)
                    return cachedArtists;
            }
            catch
            {
                // Failed to deserialize, continue to fetch fresh data
            }
        }
        
        // Get token and set authorization header
        var token = await _authService.GetToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        // Call the API endpoint
        var relatedArtists = await _httpClient.GetFromJsonAsync<List<dynamic>>($"api/spotify/artists/{artistId}/related")
            ?? new List<dynamic>();
        
        // Cache the results
        if (relatedArtists.Count > 0)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                cacheKey, 
                JsonSerializer.Serialize(relatedArtists));
        }
        
        return relatedArtists;
    }
}