using System.ComponentModel.DataAnnotations;

namespace MusicApp.Entities;

public class UserPlaylist
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    [Required]
    public string? SpotifyPlaylistId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;
    
    public string? Description { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}