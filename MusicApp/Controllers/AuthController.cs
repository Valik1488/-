using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MusicApp.Data;
using MusicApp.Entities;
using MusicApp.Interfaces;

namespace MusicApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ISpotifyService _spotifyService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IConfiguration configuration,
            ISpotifyService spotifyService,
            ApplicationDbContext context,
            ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _spotifyService = spotifyService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("spotify-login")]
        public IActionResult GetSpotifyLoginUrl()
        {
            // Generate a random state to prevent CSRF attacks
            var state = GenerateRandomState();
            
            // Store the state in session or cookies
            HttpContext.Response.Cookies.Append("SpotifyAuthState", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax, // Changed from Strict to Lax to allow redirects from Spotify
                MaxAge = TimeSpan.FromMinutes(10)
            });
            
            var authorizationUrl = _spotifyService.GetAuthorizationUrl(state);
            return Ok(new { url = authorizationUrl });
        }

        [HttpGet("spotify-callback")]
        public async Task<IActionResult> SpotifyCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error = null)
        {
            _logger.LogInformation($"Received callback with state: {state}");
            
            // Check if state cookie exists
            if (!HttpContext.Request.Cookies.TryGetValue("SpotifyAuthState", out var storedState))
            {
                _logger.LogWarning("No SpotifyAuthState cookie found");
                return BadRequest("State validation failed: No state cookie found");
            }
            
            // Validate state to prevent CSRF attacks
            if (state != storedState)
            {
                _logger.LogWarning($"State mismatch. Received: {state}, Stored: {storedState}");
                return BadRequest("Invalid state parameter");
            }
            
            // Remove the state cookie
            HttpContext.Response.Cookies.Delete("SpotifyAuthState");
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError($"Spotify authorization error: {error}");
                return BadRequest($"Spotify authorization error: {error}");
            }
            
            try
            {
                var redirectUri = _configuration["Spotify:RedirectUri"];
                _logger.LogInformation($"Using redirect URI: {redirectUri}");
                
                var tokenResponse = await _spotifyService.ExchangeCodeForTokenAsync(code, redirectUri!);
                
                // Get user profile from Spotify
                var userProfile = await _spotifyService.GetUserProfileAsync(tokenResponse.AccessToken!);
                
                // Find or create user in database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.SpotifyId == userProfile.Id);
                if (user == null)
                {
                    _logger.LogInformation($"Creating new user for Spotify ID: {userProfile.Id}");
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        SpotifyId = userProfile.Id!,
                        Email = userProfile.Email!,
                        DisplayName = userProfile.DisplayName!
                    };
                    
                    _context.Users.Add(user);
                }
                else
                {
                    _logger.LogInformation($"Found existing user for Spotify ID: {userProfile.Id}");
                }
                
                // Update Spotify tokens
                user.SpotifyAccessToken = tokenResponse.AccessToken;
                user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                
                await _context.SaveChangesAsync();
                
                // Generate JWT token for the user
                var jwt = GenerateJwtToken(user);
                
                // Redirect to client with token
                var clientBaseUrl = _configuration["ClientBaseUrl"];
                _logger.LogInformation($"Redirecting to: {clientBaseUrl}/auth/spotify-success");
                return Redirect($"{clientBaseUrl}/auth/spotify-success?token={jwt}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Spotify callback");
                return BadRequest($"Error processing Spotify callback: {ex.Message}");
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest("Refresh token is required");
                
            var user = await _context.Users.FirstOrDefaultAsync(u => u.SpotifyRefreshToken == request.RefreshToken);
            if (user == null)
                return Unauthorized("Invalid refresh token");
                
            try
            {
                var tokenResponse = await _spotifyService.RefreshTokenAsync(user.SpotifyRefreshToken!);
                
                // Update user tokens
                user.SpotifyAccessToken = tokenResponse.AccessToken;
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    user.SpotifyRefreshToken = tokenResponse.RefreshToken;
                user.SpotifyTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    AccessToken = tokenResponse.AccessToken,
                    ExpiresIn = tokenResponse.ExpiresIn,
                    TokenType = tokenResponse.TokenType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return BadRequest($"Error refreshing token: {ex.Message}");
            }
        }

        private string GenerateRandomState()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("spotify_id", user.SpotifyId)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSecurityKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.Now.AddDays(7);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtIssuer"],
                audience: _configuration["JwtAudience"],
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class RefreshTokenRequest
    {
        public string? RefreshToken { get; set; }
    }
}