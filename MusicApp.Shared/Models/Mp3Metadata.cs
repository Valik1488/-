namespace MusicApp.Shared.Models;

public class Mp3Metadata
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
    public string? Genre { get; set; }
    public string? Comment { get; set; }
    public string? TrackNumber { get; set; }
    public byte[]? AlbumArt { get; set; }
    public string? AlbumArtMimeType { get; set; }
    public bool HasAlbumArt => AlbumArt != null && AlbumArt.Length > 0;
}