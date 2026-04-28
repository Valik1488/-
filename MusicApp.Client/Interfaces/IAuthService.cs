namespace MusicApp.Client.Interfaces;

public interface IAuthService
{
    Task<string> GetSpotifyLoginUrlAsync();
    Task<bool> HandleSpotifyCallbackAsync(string token);
    Task<bool> IsUserAuthenticated();
    Task<string> GetToken();
    Task Logout();
}