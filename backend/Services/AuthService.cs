using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "B2BIntegrationHub";
    public string Audience { get; set; } = "B2BIntegrationHubClients";
    public int ExpiryMinutes { get; set; } = 120;
}

public record AuthResult(string Token, AppUser User);

/// <summary>
/// Thrown when a login attempt fails. <see cref="Code"/> tells the frontend which input to flag:
/// EMAIL_NOT_FOUND or WRONG_PASSWORD.
/// </summary>
public class LoginFailedException : Exception
{
    public string Code { get; }

    public LoginFailedException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public interface IAuthService
{
    Task<AppUser> RegisterAsync(string fullName, string email, string password, UserRole role);
    Task<AuthResult?> LoginAsync(string email, string password);
}

public class AuthService : IAuthService
{
    private readonly MongoDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(MongoDbContext context, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AppUser> RegisterAsync(string fullName, string email, string password, UserRole role)
    {
        // Always compare/store the lowercase email so "A@x.com" and "a@x.com" are the same account.
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await _context.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
        if (existing != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new AppUser
        {
            FullName = fullName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        await _context.Users.InsertOneAsync(user);
        return user;
    }

    public async Task<AuthResult?> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _context.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new LoginFailedException("EMAIL_NOT_FOUND", "No account found with this email address.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new LoginFailedException("WRONG_PASSWORD", "Incorrect password. Please try again.");
        }

        var token = GenerateToken(user);
        return new AuthResult(token, user);
    }

    private string GenerateToken(AppUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}