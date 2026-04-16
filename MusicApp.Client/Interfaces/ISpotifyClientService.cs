using MusicApp.Shared.Models;

namespace MusicApp.Client.Interfaces;

public interface ISpotifyClientService
{
    Task<SpotifyUserProfile> GetUserProfileAsync();
    Task<List<SpotifyPlaylistDto>> GetUserPlaylistsAsync();
    Task<SpotifyPlaylistDto> GetPlaylistByIdAsync(string playlistId);
    Task<List<SpotifyTrackDto>> GetPlaylistRecommendationsAsync(string playlistId);
    Task ClearStoredRecommendations(string playlistId);
    Task<bool> HasStoredRecommendations(string playlistId);
    Task<List<SpotifyArtistDto>> SearchArtistsAsync(string query);
    Task<List<dynamic>> GetRelatedArtistsAsync(string artistId);
}