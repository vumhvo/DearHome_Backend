using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DearHome_Backend.Config;
using DearHome_Backend.Models;
using DearHome_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DearHome_Backend.Services.Implementations;

/// <summary>
/// Issues the JWTs returned by login endpoints. Claim names are part of the frontend contract; do not rename them.
/// </summary>
public class TokenService : ITokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(10);

    private readonly UserManager<User> _userManager;
    private readonly JwtOptions _jwtOptions;

    public TokenService(UserManager<User> userManager, IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
    }

    public async Task<string> CreateTokenAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtKey = _jwtOptions.Key ?? throw new InvalidOperationException("JWT key is not configured.");
        var jwtIssuer = _jwtOptions.Issuer ?? throw new InvalidOperationException("JWT issuer is not configured.");
        var jwtAudience = _jwtOptions.Audience ?? throw new InvalidOperationException("JWT audience is not configured.");
        var key = Encoding.ASCII.GetBytes(jwtKey);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
        };
        // One role claim per role so [Authorize(Roles = "...")] works.
        // A single role still serializes as a plain string ("role": "Admin").
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.Add(new Claim("imageUrl", user.ImageUrl ?? string.Empty));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            Expires = DateTime.UtcNow.Add(TokenLifetime),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
