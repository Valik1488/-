using Microsoft.AspNetCore.Mvc;
using MusicApp.Shared.Models;
using Microsoft.AspNetCore.Http;
using MusicApp.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MusicApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class Mp3Controller : ControllerBase
    {
        private readonly IMp3MetadataService _mp3MetadataService;
        private readonly ILogger<Mp3Controller> _logger;

        public Mp3Controller(IMp3MetadataService mp3MetadataService, ILogger<Mp3Controller> logger)
        {
            _mp3MetadataService = mp3MetadataService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> UploadMp3()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var file = form.Files.GetFile("file");
                
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                if (!file.ContentType.Equals("audio/mpeg") && !Path.GetExtension(file.FileName).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only MP3 files are allowed");

                // Read the metadata
                var metadata = await _mp3MetadataService.ReadMetadataAsync(file);
                
                // If Title is empty, use the filename without extension
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    metadata.Title = Path.GetFileNameWithoutExtension(file.FileName);
                }
                
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading MP3 file");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("update")]
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> UpdateMetadata()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var file = form.Files.GetFile("File");
                var albumArtFile = form.Files.GetFile("AlbumArtFile");
                
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                if (!file.ContentType.Equals("audio/mpeg") && !Path.GetExtension(file.FileName).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only MP3 files are allowed");

                // Extract metadata from form
                var metadata = new Mp3Metadata
                {
                    Title = form["Metadata.Title"].ToString(),
                    Artist = form["Metadata.Artist"].ToString(),
                    Album = form["Metadata.Album"].ToString(),
                    Year = form["Metadata.Year"].ToString(),
                    Genre = form["Metadata.Genre"].ToString(),
                    Comment = form["Metadata.Comment"].ToString(),
                    TrackNumber = form["Metadata.TrackNumber"].ToString()
                };

                // Handle album art
                var removeAlbumArt = form.ContainsKey("RemoveAlbumArt") && 
                                    bool.TryParse(form["RemoveAlbumArt"], out bool remove) && 
                                    remove;

                if (removeAlbumArt)
                {
                    metadata.AlbumArt = null;
                }
                else if (albumArtFile == null && form.ContainsKey("Metadata.PreserveExistingAlbumArt") && 
                        bool.TryParse(form["Metadata.PreserveExistingAlbumArt"], out bool preserve) && 
                        preserve)
                {
                    // Extract existing album art to preserve it
                    metadata.AlbumArt = await _mp3MetadataService.ExtractAlbumArtAsync(file);
                    metadata.AlbumArtMimeType = "image/jpeg"; // Assume JPEG, most common format
                }

                // Update metadata and get the modified file
                var result = await _mp3MetadataService.UpdateMetadataAsync(file, metadata, albumArtFile);
                
                // Return the modified file
                return File(result.FileContent!, "audio/mpeg", result.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating MP3 metadata");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("albumArt/{tempId}")]
        public IActionResult GetAlbumArt(string tempId)
        {
            if (string.IsNullOrEmpty(tempId) || !HttpContext.Session.TryGetValue($"albumArt_{tempId}", out byte[]? albumArtData) 
                || albumArtData == null)
            {
                return NotFound();
            }

            string mimeType = "image/jpeg"; // Default to JPEG
            if (HttpContext.Session.TryGetValue($"albumArtMime_{tempId}", out byte[]? mimeBytes) && mimeBytes != null)
            {
                mimeType = System.Text.Encoding.UTF8.GetString(mimeBytes);
            }

            return File(albumArtData, mimeType);
        }
    }
}