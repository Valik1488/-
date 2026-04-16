using MusicApp.Shared.Models;

namespace MusicApp.Interfaces
{
    public interface ISpotifyService
    {
        string GetAuthorizationUrl(string state);
        Task<SpotifyTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri);
        Task<SpotifyTokenResponse> RefreshTokenAsync(string refreshToken);
        Task<SpotifyUserProfile> GetUserProfileAsync(string accessToken);
        Task<List<SpotifyPlaylistDto>> GetUserPlaylistsAsync(string accessToken);
        Task<List<SpotifyTrackDto>> GetPlaylistTracksAsync(string accessToken, string playlistId);
        Task<List<SpotifyArtistDto>> GetArtistsByGenreAsync(string accessToken, string genre, int limit = 5);
        Task<List<SpotifyTrackDto>> GetArtistTopTracksAsync(string accessToken, string artistId, string market = "US");
        Task<List<SpotifyTrackDto>> GetPlaylistRecommendationsAsync(string accessToken, string playlistId);
        Task<List<SpotifyArtistDto>> SearchArtistsAsync(string accessToken, string query);
        Task<List<string>> GetArtistAlbumsAsync(string accessToken, string artistId);
        Task<List<RelatedArtistDto>> GetRelatedArtistsAsync(string accessToken, string artistId);
        Task<List<SpotifyArtistDto>> GetArtistsDataAsync(string accessToken, List<string> artistIds);
    }
}