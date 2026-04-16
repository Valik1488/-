using MusicApp.Shared.Models;

namespace MusicApp.Interfaces
{
    public interface IMp3MetadataService
    {
        Task<Mp3Metadata> ReadMetadataAsync(IFormFile file);
        Task<Mp3FileResult> UpdateMetadataAsync(IFormFile file, Mp3Metadata metadata, IFormFile? albumArtFile = null);
        Task<byte[]?> ExtractAlbumArtAsync(IFormFile file);
    }
}