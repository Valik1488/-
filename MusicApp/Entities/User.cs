using System.ComponentModel.DataAnnotations;

namespace MusicApp.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [StringLength(50)]
    public string DisplayName { get; set; } = null!;

    // Spotify-related properties
    public string SpotifyId { get; set; } = null!;
    public string? SpotifyAccessToken { get; set; }
    public string? SpotifyRefreshToken { get; set; }
    public DateTime? SpotifyTokenExpiry { get; set; }

    public virtual ICollection<UserPlaylist> Playlists { get; set; } = new List<UserPlaylist>();
}