using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using MusicApp.Shared.Models;
using MusicApp.Interfaces;

namespace MusicApp.Services
{
    public class SpotifyService : ISpotifyService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string? _clientId;
        private readonly string? _clientSecret;
        private readonly string _authorizationEndpoint = "https://accounts.spotify.com/authorize";
        private readonly string _tokenEndpoint = "https://accounts.spotify.com/api/token";
        private readonly string _apiBaseUrl = "https://api.spotify.com/v1";

        public SpotifyService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _clientId = configuration["Spotify:ClientId"];
            _clientSecret = configuration["Spotify:ClientSecret"];
        }

        public string GetAuthorizationUrl(string state)
        {
            var scopes = new[] 
            {
                "user-read-private",
                "user-read-email",
                "playlist-read-private",
                "playlist-read-collaborative"
            };

            var queryParams = new Dictionary<string, string>
            {
                { "client_id", _clientId! },
                { "response_type", "code" },
                { "redirect_uri", _configuration["Spotify:RedirectUri"]! },
                { "scope", string.Join(" ", scopes) },
                { "state", state }
            };

            var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
            return $"{_authorizationEndpoint}?{queryString}";
        }

        public async Task<SpotifyTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri)
{
    var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);

    var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", code },
        { "redirect_uri", redirectUri },
        { "client_id", _clientId! },
        { "client_secret", _clientSecret! }
    });

    request.Content = requestContent;

    var response = await _httpClient.SendAsync(request);

    var responseContent = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception(responseContent);
    }

    var token = JsonSerializer.Deserialize<SpotifyTokenResponse>(
        responseContent,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    ) ?? throw new Exception("Failed to parse token");

    return token;
}
        public async Task<SpotifyTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", _clientId! },
                { "client_secret", _clientSecret! }
            });
            
            request.Content = content;
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? throw new InvalidOperationException("Failed to deserialize Spotify token response.");
            return tokenResponse;
        }

        public async Task<SpotifyUserProfile> GetUserProfileAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var userProfile = JsonSerializer.Deserialize<SpotifyUserProfile>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? throw new InvalidOperationException("Failed to deserialize Spotify user profile.");
            
            return userProfile;
        }

        public async Task<List<SpotifyPlaylistDto>> GetUserPlaylistsAsync(string accessToken)
        {
            var playlists = new List<SpotifyPlaylistDto>();
            string nextUrl = $"{_apiBaseUrl}/me/playlists?limit=50";
            
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var playlistResponse = JsonSerializer.Deserialize<SpotifyPaginatedResponse<SpotifyPlaylistDto>>(
                    content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                
                playlists.AddRange(playlistResponse!.Items!);
                nextUrl = playlistResponse.Next!;
            }
            
            return playlists;
        }

        public async Task<List<SpotifyTrackDto>> GetPlaylistTracksAsync(string accessToken, string playlistId)
        {
            var tracks = new List<SpotifyTrackDto>();
            string nextUrl = $"{_apiBaseUrl}/playlists/{playlistId}/tracks?limit=100";
            
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var tracksResponse = JsonSerializer.Deserialize<SpotifyPaginatedResponse<SpotifyPlaylistTrackDto>>(
                    content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                
                // Extract track information from playlist track objects
                if (tracksResponse?.Items != null)
                {
                    tracks.AddRange(tracksResponse.Items
                        .Where(item => item.Track != null)
                        .Select(item => item.Track!));
                }
                
                nextUrl = tracksResponse?.Next ?? string.Empty;
            }
            
            return tracks;
        }

        public async Task<List<SpotifyArtistDto>> GetArtistsByGenreAsync(string accessToken, string genre, int limit = 5)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/search?q=genre:{HttpUtility.UrlEncode(genre)}&type=artist&limit={limit}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(
                content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            
            return searchResponse?.Artists?.Items?.Select(a => new SpotifyArtistDto
            {
                Id = a.Id,
                Name = a.Name,
                Popularity = a.Popularity,
                Genres = a.Genres,
                Images = a.Images?.Select(img => new SpotifyImage
                {
                    Url = img.Url,
                    Height = img.Height,
                    Width = img.Width
                }).ToList() ?? new List<SpotifyImage>(),
                ExternalUrls = a.ExternalUrls
            }).ToList() ?? new List<SpotifyArtistDto>();
        }

        public async Task<List<SpotifyTrackDto>> GetArtistTopTracksAsync(string accessToken, string artistId, string market = "US")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/artists/{artistId}/top-tracks?market={market}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var topTracksResponse = JsonSerializer.Deserialize<SpotifyArtistTopTracksResponse>(
                content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            
            return topTracksResponse?.Tracks ?? new List<SpotifyTrackDto>();
        }

        public async Task<List<SpotifyTrackDto>> GetPlaylistRecommendationsAsync(string accessToken, string playlistId)
        {
            // Get tracks from the playlist
            var playlistTracks = await GetPlaylistTracksAsync(accessToken, playlistId);
            
            // Sample tracks if the playlist is large
            var sampledTracks = playlistTracks.Count > 100 
                ? playlistTracks.OrderBy(_ => Guid.NewGuid()).Take(100).ToList() 
                : playlistTracks;
            
            // Extract unique artist IDs
            var uniqueArtistIds = new HashSet<string>();
            foreach (var track in sampledTracks)
            {
                if (track.Artists != null)
                {
                    foreach (var artist in track.Artists)
                    {
                        if (artist != null && !string.IsNullOrEmpty(artist.Id))
                        {
                            uniqueArtistIds.Add(artist.Id);
                        }
                    }
                }
            }
            
            // Get artist details in batches of 50 (Spotify's limit for Get Several Artists endpoint)
            var genreCounts = new Dictionary<string, int>();
            var artistIds = uniqueArtistIds.ToList();
            
            for (int i = 0; i < artistIds.Count; i += 50)
            {
                var batchIds = artistIds.Skip(i).Take(50);
                var idsParam = string.Join(",", batchIds);
                
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/artists?ids={idsParam}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var artistsResponse = JsonSerializer.Deserialize<SpotifyMultipleArtistsResponse>(
                        content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (artistsResponse?.Artists != null)
                    {
                        foreach (var artist in artistsResponse.Artists)
                        {
                            if (artist?.Genres != null)
                            {
                                foreach (var genre in artist.Genres)
                                {
                                    if (!string.IsNullOrEmpty(genre))
                                    {
                                        if (genreCounts.ContainsKey(genre))
                                            genreCounts[genre]++;
                                        else
                                            genreCounts[genre] = 1;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Continue with next batch if we can't get details for one batch
                    continue;
                }
            }
            
            // Get top 5 most common genres
            var topGenres = genreCounts
                .OrderByDescending(g => g.Value)
                .Take(5)
                .Select(g => g.Key)
                .ToList();
            
            // Create recommendations list
            var recommendations = new List<SpotifyTrackDto>();
            var processedArtists = new HashSet<string>();
            
            // For each genre, find artists and get their top tracks
            foreach (var genre in topGenres)
            {
                var artists = await GetArtistsByGenreAsync(accessToken, genre, 3);
                
                // Find artists we haven't processed yet
                foreach (var artist in artists)
                {
                    if (artist != null && !string.IsNullOrEmpty(artist.Id) && !processedArtists.Contains(artist.Id))
                    {
                        processedArtists.Add(artist.Id);
                        var topTracks = await GetArtistTopTracksAsync(accessToken, artist.Id);
                        
                        // Add top tracks to recommendations
                        recommendations.AddRange(topTracks.Take(2));
                        
                        // Break after processing one artist per genre to limit API calls
                        break;
                    }
                }
            }
            
            // Randomize and limit recommendations
            var finalRecommendations = recommendations
                .OrderBy(_ => Guid.NewGuid())
                .Take(15)
                .ToList();
                
            // Make sure all tracks have album artwork data
            await EnsureTrackAlbumImagesAsync(accessToken, finalRecommendations);
                
            return finalRecommendations;
        }
        
        // New method to ensure tracks have album images
        private async Task EnsureTrackAlbumImagesAsync(string accessToken, List<SpotifyTrackDto> tracks)
        {
            var tracksMissingAlbumImages = tracks
                .Where(t => t.Album?.Images == null || !t.Album.Images.Any())
                .ToList();
                
            if (!tracksMissingAlbumImages.Any())
                return; // All tracks have images, nothing to do
                
            // Get album IDs that need image data
            var albumIds = tracksMissingAlbumImages
                .Where(t => t.Album != null && !string.IsNullOrEmpty(t.Album.Id))
                .Select(t => t.Album!.Id!)
                .Distinct()
                .ToList();
                
            if (!albumIds.Any())
                return; // No valid album IDs to fetch
                
            // Process in batches of 20 (Spotify's limit for Get Multiple Albums endpoint)
            for (int i = 0; i < albumIds.Count; i += 20)
            {
                var batchIds = albumIds.Skip(i).Take(20);
                var idsParam = string.Join(",", batchIds);
                
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/albums?ids={idsParam}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var albumsResponse = JsonSerializer.Deserialize<SpotifyMultipleAlbumsResponse>(
                        content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (albumsResponse?.Albums != null)
                    {
                        // Create lookup for faster access
                        var albumLookup = albumsResponse.Albums
                            .Where(a => a != null && !string.IsNullOrEmpty(a.Id))
                            .ToDictionary(a => a.Id!);
                            
                        // Update tracks with album data
                        foreach (var track in tracksMissingAlbumImages)
                        {
                            if (track.Album != null && !string.IsNullOrEmpty(track.Album.Id) && 
                                albumLookup.TryGetValue(track.Album.Id, out var albumDetails))
                            {
                                track.Album.Images = albumDetails.Images;
                                track.Album.Name = albumDetails.Name;
                                track.Album.ReleaseDate = albumDetails.ReleaseDate;
                            }
                            else if (track.Album != null && (track.Album.Images == null || !track.Album.Images.Any()))
                            {
                                // Provide default image if we couldn't get album data
                                track.Album.Images = new List<SpotifyImage> {
                                    new SpotifyImage { 
                                        Url = "/images/default-album.png",
                                        Height = 300,
                                        Width = 300
                                    }
                                };
                            }
                        }
                    }
                }
                catch
                {
                    // Continue with next batch if we can't get details for one batch
                    continue;
                }
            }
            
            // Ensure all tracks have at least a default image
            foreach (var track in tracks)
            {
                if (track.Album == null)
                {
                    track.Album = new SpotifyAlbumDto { 
                        Name = "Unknown Album",
                        Images = new List<SpotifyImage> {
                            new SpotifyImage { 
                                Url = "/images/default-album.png",
                                Height = 300,
                                Width = 300
                            }
                        }
                    };
                }
                else if (track.Album.Images == null || !track.Album.Images.Any())
                {
                    track.Album.Images = new List<SpotifyImage> {
                        new SpotifyImage { 
                            Url = "/images/default-album.png",
                            Height = 300,
                            Width = 300
                        }
                    };
                }
                
                // Ensure track has artists data
                if (track.Artists == null || !track.Artists.Any())
                {
                    track.Artists = new List<SpotifyArtistDto> {
                        new SpotifyArtistDto { Name = "Unknown Artist" }
                    };
                }
            }
        }

        public async Task<List<SpotifyArtistDto>> SearchArtistsAsync(string accessToken, string query)
        {
            // Create an HttpClient with the appropriate authorization header
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            try
            {
                // Encode the query parameter properly
                var encodedQuery = Uri.EscapeDataString(query);
                var response = await httpClient.GetAsync($"https://api.spotify.com/v1/search?q={encodedQuery}&type=artist&limit=20");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = System.Text.Json.JsonSerializer.Deserialize<SpotifySearchResponse>(content, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (searchResponse?.Artists?.Items == null)
                    return new List<SpotifyArtistDto>();
                    
                // Map the response to our DTO model
                var artists = searchResponse.Artists.Items.Select(a => new SpotifyArtistDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Popularity = a.Popularity,
                    Genres = a.Genres,
                    Images = a.Images?.Select(img => new SpotifyImage
                    {
                        Url = img.Url,
                        Height = img.Height,
                        Width = img.Width
                    }).ToList() ?? new List<SpotifyImage>(),
                    ExternalUrls = a.ExternalUrls
                }).ToList();
                
                return artists;
            }
            catch (Exception ex)
            {
                // Log the exceptionАа
                Console.WriteLine($"Error searching for artists: {ex.Message}");
                return new List<SpotifyArtistDto>();
            }
        }


        public async Task<List<string>> GetArtistAlbumsAsync(string accessToken, string artistId)
        {
            var albumIds = new List<string>();
            string nextUrl = $"{_apiBaseUrl}/artists/{artistId}/albums?limit=50&include_groups=album,single,appears_on";
            
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var albumsResponse = JsonSerializer.Deserialize<SpotifyPaginatedResponse<SpotifyAlbumDto>>(
                    content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                
                if (albumsResponse?.Items != null)
                {
                    albumIds.AddRange(albumsResponse.Items
                        .Where(album => !string.IsNullOrEmpty(album.Id))
                        .Select(album => album.Id!));
                }
                
                nextUrl = albumsResponse?.Next ?? string.Empty;
            }
            
            return albumIds;
        }
        
        public async Task<List<RelatedArtistDto>> GetRelatedArtistsFromAlbumsAsync(
            string accessToken, 
            List<string> albumIds, 
            string artistId)
        {
            var connectedArtists = new HashSet<string>();
            var connectedArtistsData = new List<RelatedArtistDto>();
            
            // Process albums in batches of 20 (Spotify API limit)
            for (int i = 0; i < albumIds.Count; i += 20)
            {
                var batch = albumIds.Skip(i).Take(20).ToList();
                var idsParam = string.Join(",", batch);
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/albums?ids={idsParam}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                try
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    // Handle API rate limiting
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // Get retry-after header or default to 5 seconds
                        int retryAfter = 5;
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                        {
                            retryAfter = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                        }
                        
                        await Task.Delay(retryAfter * 1000);
                        i -= 20; // Retry this batch
                        continue;
                    }
                    
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var albumsResponse = JsonSerializer.Deserialize<SpotifyMultipleAlbumsResponse>(
                        content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (albumsResponse?.Albums != null)
                    {
                        foreach (var album in albumsResponse.Albums)
                        {
                            if (album?.Tracks?.Items == null) continue;
                            
                            foreach (var track in album.Tracks.Items)
                            {
                                if (track?.Artists == null) continue;
                                
                                var artistIds = track.Artists.Select(a => a.Id).ToList();
                                
                                // Only process tracks where the requested artist is a collaborator
                                if (artistIds.Count > 1 && artistIds.Contains(artistId))
                                {
                                    int index = 0;
                                    foreach (var trackArtist in track.Artists)
                                    {
                                        var newArtistId = trackArtist.Id;
                                        
                                        // Skip the original artist and artists we've already found
                                        if (newArtistId != artistId && !connectedArtists.Contains(newArtistId!))
                                        {
                                            // Create embed URL from track URL
                                            string embedUrl = string.Empty;
                                            if (!string.IsNullOrEmpty(track.ExternalUrls?.Spotify))
                                            {
                                                var trackParts = track.ExternalUrls.Spotify.Split("/track/");
                                                if (trackParts.Length > 1)
                                                {
                                                    embedUrl = $"{trackParts[0]}/embed/track/{trackParts[1]}";
                                                }
                                            }
                                            
                                            connectedArtists.Add(newArtistId!);
                                            connectedArtistsData.Add(new RelatedArtistDto
                                            {
                                                ArtistId = newArtistId!,
                                                ArtistName = trackArtist.Name!,
                                                TrackName = $"{track.Name} by {album.Artists?.FirstOrDefault()?.Name ?? "Unknown"}",
                                                TrackURL = track.PreviewUrl,
                                                TrackLink = embedUrl
                                            });
                                        }
                                        index++;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log exception and continue with next batch
                    Console.WriteLine($"Error fetching albums: {ex.Message}");
                    continue;
                }
            }
            
            return connectedArtistsData;
        }

        public async Task<List<SpotifyArtistDto>> GetArtistsDataAsync(string accessToken, List<string> artistIds)
        {
            var artistData = new List<SpotifyArtistDto>();
            
            // Process artists in batches of 50 (Spotify API limit)
            for (int i = 0; i < artistIds.Count; i += 50)
            {
                var batch = artistIds.Skip(i).Take(50).ToList();
                var idsParam = string.Join(",", batch);
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/artists?ids={idsParam}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                try
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    // Handle API rate limiting
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // Get retry-after header or default to 5 seconds
                        int retryAfter = 5;
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                        {
                            retryAfter = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                        }
                        
                        await Task.Delay(retryAfter * 1000);
                        i -= 50; // Retry this batch
                        continue;
                    }
                    
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var artistsResponse = JsonSerializer.Deserialize<SpotifyMultipleArtistsResponse>(
                        content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (artistsResponse?.Artists != null)
                    {
                        artistData.AddRange(artistsResponse.Artists.Select(a => new SpotifyArtistDto
                        {
                            Id = a.Id,
                            Name = a.Name,
                            Popularity = a.Popularity,
                            Images = a.Images,
                            Genres = a.Genres,
                            ExternalUrls = a.ExternalUrls
                        }));
                    }
                }
                catch (Exception ex)
                {
                    // Log exception and continue with next batch
                    Console.WriteLine($"Error fetching artists: {ex.Message}");
                    continue;
                }
            }
            
            return artistData;
        }
        
        public async Task<List<RelatedArtistDto>> GetRelatedArtistsAsync(string accessToken, string artistId)
        {
            // Step 1: Get all albums for the artist
            var albumIds = await GetArtistAlbumsAsync(accessToken, artistId);
            
            // Step 2: Process these albums to find collaborations
            var relatedArtists = await GetRelatedArtistsFromAlbumsAsync(accessToken, albumIds, artistId);
            
            return relatedArtists;
        }

        // Add this class to handle the Spotify search response
        public class SpotifySearchResponse
        {
            public SpotifyArtistsResponse? Artists { get; set; }
            
            public class SpotifyArtistsResponse
            {
                public List<SpotifyArtistResponse>? Items { get; set; }
            }
            
            public class SpotifyArtistResponse
            {
                public string Id { get; set; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public int Popularity { get; set; }
                public List<string> Genres { get; set; } = new();
                public List<SpotifyImageResponse>? Images { get; set; }
                public Dictionary<string, string>? ExternalUrls { get; set; }
            }
            
            public class SpotifyImageResponse
            {
                public string Url { get; set; } = string.Empty;
                public int? Height { get; set; }
                public int? Width { get; set; }
            }
        }
        
        private async Task<SpotifyArtistDto> GetArtistAsync(string accessToken, string artistId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/artists/{artistId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SpotifyArtistDto>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        // Add this class to support the new batched artist request
        public class SpotifyMultipleArtistsResponse
        {
            public List<SpotifyArtistDto>? Artists { get; set; }
        }
        
        // Add this class to support the multiple albums request
        public class SpotifyMultipleAlbumsResponse
        {
            public List<SpotifyAlbumDto>? Albums { get; set; }
        }

        public List<SpotifyArtistDto> AnalyzeArtistsDeep(List<SpotifyArtistDto> artists)
{
    if (artists == null || artists.Count == 0)
        return artists ?? new List<SpotifyArtistDto>();

    try
    {
        var genreFrequency = new Dictionary<string, int>();

        foreach (var a in artists)
        {
            if (a.Genres == null) continue;

            foreach (var g in a.Genres)
            {
                if (string.IsNullOrWhiteSpace(g)) continue;

                if (genreFrequency.ContainsKey(g))
                    genreFrequency[g]++;
                else
                    genreFrequency[g] = 1;
            }
        }

        var result = artists
            .Select(a =>
            {
                double popularity = a.Popularity ?? 0;
                int genreCount = a.Genres?.Count ?? 0;

                double rarity = 0;

                if (a.Genres != null)
                {
                    foreach (var g in a.Genres)
                    {
                        if (genreFrequency.TryGetValue(g, out var count) && count > 0)
                            rarity += 1.0 / count;
                    }
                }

                double score =
                    (popularity * 0.5) +
                    (genreCount * 10 * 0.3) +
                    (rarity * 50 * 0.2);

                return new { a, score };
            })
            .OrderByDescending(x => x.score)
            .Select(x => x.a)
            .ToList();

        return result;
    }
    catch
    {
        return artists;
    }
}
    }
}