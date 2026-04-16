using System.Text.Json.Serialization;

namespace MusicApp.Shared.Models
{
    public class SpotifyTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
    public class SpotifyExternalUrlsDto
    {
        [JsonPropertyName("spotify")]
        public string? Spotify { get; set; }
    }

    public class SpotifyUserProfile
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("images")]
        public List<SpotifyImage>? Images { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("product")]
        public string? Product { get; set; }
    }

    public class SpotifyImage
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }
    }

    public class SpotifyPlaylistDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("images")]
        public List<SpotifyImage>? Images { get; set; }

        [JsonPropertyName("tracks")]
        public SpotifyTracksReference? Tracks { get; set; }
    }

    public class SpotifyTracksReference
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class SpotifyPaginatedResponse<T>
    {
        [JsonPropertyName("items")]
        public List<T>? Items { get; set; }

        [JsonPropertyName("next")]
        public string? Next { get; set; }

        [JsonPropertyName("previous")]
        public string? Previous { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class SpotifyLoginUrlResponse
    {
        public string? Url { get; set; }
    }

    public class SpotifyPlaylistTrackDto
    {
        public SpotifyTrackDto? Track { get; set; }
        public DateTime? AddedAt { get; set; }
    }

    public class SpotifyTrackDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Popularity { get; set; }
        public List<SpotifyArtistDto>? Artists { get; set; }
        public SpotifyAlbumDto? Album { get; set; }
        public SpotifyTrackFeaturesDto? Features { get; set; }
        public string? PreviewUrl { get; set; }
        public int? DurationMs { get; set; }
        public string? Uri { get; set; }
        public SpotifyExternalUrlsDto? ExternalUrls { get; set; }
    }

    public class SpotifyArtistDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Genres { get; set; }
        public int? Popularity { get; set; }
        public List<SpotifyImage>? Images { get; set; }
        public Dictionary<string, string>? ExternalUrls { get; set; }
    }

    public class SpotifyAlbumDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? AlbumType { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ReleaseDatePrecision { get; set; }
        public List<SpotifyImage>? Images { get; set; }
        public List<SpotifyArtistDto>? Artists { get; set; }
        public SpotifyExternalUrlsDto? ExternalUrls { get; set; }
        public SpotifyTracksResponse? Tracks { get; set; }
        
        public class SpotifyTracksResponse
        {
            public List<SpotifyTrackDto>? Items { get; set; }
        }
    }

    public class SpotifyTrackFeaturesDto
    {
        public float? Danceability { get; set; }
        public float? Energy { get; set; }
        public float? Speechiness { get; set; }
        public float? Acousticness { get; set; }
        public float? Instrumentalness { get; set; }
        public float? Liveness { get; set; }
        public float? Valence { get; set; }
        public float? Tempo { get; set; }
        public int? TimeSignature { get; set; }
        public int? Key { get; set; }
        public int? Mode { get; set; }
    }

    public class SpotifySearchResponse
    {
        public SpotifyPaginatedResponse<SpotifyArtistDto>? Artists { get; set; }
        public SpotifyPaginatedResponse<SpotifyTrackDto>? Tracks { get; set; }
        public SpotifyPaginatedResponse<SpotifyAlbumDto>? Albums { get; set; }
    }

    public class SpotifyArtistTopTracksResponse
    {
        public List<SpotifyTrackDto>? Tracks { get; set; }
    }

    public class ArtistCollaborationNetwork
    {
        public List<ArtistNode> Nodes { get; set; } = new List<ArtistNode>();
        public List<ArtistCollaboration> Links { get; set; } = new List<ArtistCollaboration>();
    }

    public class ArtistNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new List<string>();
        public string ImageUrl { get; set; } = string.Empty;
        public int Popularity { get; set; }
        public int Group { get; set; } // For visualization grouping
    }

    public class ArtistCollaboration
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public int Weight { get; set; } = 1; // Number of collaborations
        public List<string> Tracks { get; set; } = new List<string>(); // Track names they collaborated on
    }

    public class RelatedArtistDto
    {
        public string ArtistId { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string? TrackURL { get; set; }
        public string TrackLink { get; set; } = string.Empty;
    }
}