using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MusicApp.Data;
using MusicApp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using MusicApp.Shared.Models;

namespace MusicApp.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SpotifyController : ControllerBase
    {
        private readonly ISpotifyService _spotifyService;
        private readonly ApplicationDbContext _context;

        public SpotifyController(
            ISpotifyService spotifyService,
            ApplicationDbContext context)
        {
            _spotifyService = spotifyService;
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                var profile = await _spotifyService.GetUserProfileAsync(user.SpotifyAccessToken!);
                return Ok(profile);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving Spotify profile: {ex.Message}");
            }
        }

        [HttpGet("playlists")]
        public async Task<IActionResult> GetUserPlaylists()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                var playlists = await _spotifyService.GetUserPlaylistsAsync(user.SpotifyAccessToken!);
                return Ok(playlists);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving Spotify playlists: {ex.Message}");
            }
        }
        
        [HttpGet("playlists/{playlistId}")]
        public async Task<IActionResult> GetPlaylistById(string playlistId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                // First try to get from all playlists
                var playlists = await _spotifyService.GetUserPlaylistsAsync(user.SpotifyAccessToken!);
                var playlist = playlists.FirstOrDefault(p => p.Id == playlistId);
                
                if (playlist == null)
                {
                    return NotFound($"Playlist with ID {playlistId} not found");
                }
                
                return Ok(playlist);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving playlist: {ex.Message}");
            }
        }

        [HttpGet("playlists/{playlistId}/recommendations")]
        public async Task<IActionResult> GetPlaylistRecommendations(string playlistId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                var recommendations = await _spotifyService.GetPlaylistRecommendationsAsync(user.SpotifyAccessToken!, playlistId);
                
                // Validate recommendations
                foreach (var track in recommendations)
                {
                    // Ensure all tracks have the required properties
                    EnsureTrackProperties(track);
                }
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving recommendations: {ex.Message}");
            }
        }
        
        // Helper method to ensure all track properties are valid
        private void EnsureTrackProperties(SpotifyTrackDto track)
        {
            // Ensure Album exists and has images
            if (track.Album == null)
            {
                track.Album = new SpotifyAlbumDto 
                { 
                    Name = "Unknown Album",
                    Images = new List<SpotifyImage> 
                    { 
                        new SpotifyImage 
                        { 
                            Url = "/images/default-album.png",
                            Height = 300,
                            Width = 300
                        } 
                    }
                };
            }
            else if (track.Album.Images == null || track.Album.Images.Count == 0)
            {
                track.Album.Images = new List<SpotifyImage> 
                { 
                    new SpotifyImage 
                    { 
                        Url = "/images/default-album.png",
                        Height = 300,
                        Width = 300
                    } 
                };
            }
            
            // Ensure Artists exists
            if (track.Artists == null || track.Artists.Count == 0)
            {
                track.Artists = new List<SpotifyArtistDto> 
                { 
                    new SpotifyArtistDto { Name = "Unknown Artist" } 
                };
            }
            
            // Ensure track has a name
            if (string.IsNullOrEmpty(track.Name))
            {
                track.Name = "Unknown Track";
            }
        }

        [HttpGet("search/artists")]
        public async Task<IActionResult> SearchArtists([FromQuery] string query)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return Ok(new List<SpotifyArtistDto>());
                
                var artists = await _spotifyService.SearchArtistsAsync(user.SpotifyAccessToken!, query);
                return Ok(artists);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error searching for artists: {ex.Message}");
            }
        }

        [HttpGet("artists/{artistId}/related")]
        public async Task<IActionResult> GetRelatedArtists(string artistId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userGuid);
            if (user == null)
                return NotFound("User not found");

            // Check if token is expired, refresh if needed
            if (user.SpotifyTokenExpiry <= DateTime.UtcNow)
            {
                try
                {
                    var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                    user.SpotifyAccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                    user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return Unauthorized($"Failed to refresh token: {ex.Message}");
                }
            }

            try
            {
                var relatedArtists = await _spotifyService.GetRelatedArtistsAsync(user.SpotifyAccessToken!, artistId);
                
                // Get full artist information for the related artists
                if (relatedArtists.Any())
                {
                    var artistIds = relatedArtists.Select(a => a.ArtistId).Distinct().ToList();
                    var artistDetails = await _spotifyService.GetArtistsDataAsync(user.SpotifyAccessToken!, artistIds);
                    
                    // Enrich related artists with additional artist information
                    var result = relatedArtists.Select(ra => {
                        var details = artistDetails.FirstOrDefault(ad => ad.Id == ra.ArtistId);
                        return new 
                        {
                            ra.ArtistId,
                            ra.ArtistName,
                            ra.TrackName,
                            ra.TrackURL,
                            ra.TrackLink,
                            Popularity = details?.Popularity ?? 0,
                            Image = details?.Images?.FirstOrDefault()?.Url,
                            Genres = details?.Genres
                        };
                    }).ToList();
                    
                    return Ok(result);
                }
                
                return Ok(relatedArtists);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving related artists: {ex.Message}");
            }
        }
    }
}