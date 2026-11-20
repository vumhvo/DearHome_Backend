using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DearHome_Backend.Config;
using DearHome_Backend.Models;
using DearHome_Backend.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace DearHome_Backend.Tests.Services;

public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256!!";
    private const string Issuer = "https://issuer.test/";
    private const string Audience = "https://audience.test/";

    private static readonly User TestUser = new()
    {
        Id = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
        UserName = "jane",
        Email = "jane@example.com",
        Name = "Jane Doe",
        ImageUrl = "https://cdn.test/jane.png"
    };

    private static TokenService CreateService(IList<string> roles, JwtOptions? options = null)
    {
        var userManager = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<User>())).ReturnsAsync(roles);

        return new TokenService(userManager.Object, Options.Create(options ?? new JwtOptions
        {
            Key = Key,
            Issuer = Issuer,
            Audience = Audience
        }));
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public async Task Token_contains_the_claims_the_frontend_reads_with_their_short_names()
    {
        var token = Read(await CreateService(new List<string> { "User" }).CreateTokenAsync(TestUser));

        // These names are the frontend / AI backend contract (JwtSecurityTokenHandler's outbound claim map).
        token.Claims.Should().Contain(c => c.Type == "nameid" && c.Value == TestUser.Id.ToString());
        token.Claims.Should().Contain(c => c.Type == "unique_name" && c.Value == "jane");
        token.Claims.Should().Contain(c => c.Type == "email" && c.Value == "jane@example.com");
        token.Claims.Should().Contain(c => c.Type == "role" && c.Value == "User");
        token.Claims.Should().Contain(c => c.Type == "imageUrl" && c.Value == "https://cdn.test/jane.png");
    }

    [Fact]
    public async Task Single_role_is_serialized_as_a_plain_string()
    {
        var token = Read(await CreateService(new List<string> { "Admin" }).CreateTokenAsync(TestUser));

        token.Payload["role"].Should().Be("Admin");
    }

    [Fact]
    public async Task Multiple_roles_produce_one_role_claim_each()
    {
        var token = Read(await CreateService(new List<string> { "Admin", "User" }).CreateTokenAsync(TestUser));

        token.Claims.Where(c => c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "Admin", "User" });
    }

    [Fact]
    public async Task User_without_roles_gets_no_role_claim()
    {
        var token = Read(await CreateService(new List<string>()).CreateTokenAsync(TestUser));

        token.Claims.Should().NotContain(c => c.Type == "role");
    }

    [Fact]
    public async Task Missing_image_url_is_an_empty_string_claim()
    {
        var user = new User { Id = Guid.NewGuid(), UserName = "bob", Email = "bob@example.com", Name = "Bob" };

        var token = Read(await CreateService(new List<string> { "User" }).CreateTokenAsync(user));

        token.Claims.Should().Contain(c => c.Type == "imageUrl" && c.Value == string.Empty);
    }

    [Fact]
    public async Task Token_has_configured_issuer_audience_and_ten_hour_lifetime()
    {
        var before = DateTime.UtcNow;
        var token = Read(await CreateService(new List<string> { "User" }).CreateTokenAsync(TestUser));

        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().ContainSingle().Which.Should().Be(Audience);
        token.ValidTo.Should().BeCloseTo(before.AddHours(10), TimeSpan.FromMinutes(1));
        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public async Task Token_validates_with_the_same_parameters_as_Program_cs()
    {
        var tokenString = await CreateService(new List<string> { "Admin", "User" }).CreateTokenAsync(TestUser);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Key))
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(tokenString, parameters, out _);

        // With the default inbound claim map, the short names map back to ClaimTypes, so [Authorize(Roles)] works.
        principal.IsInRole("Admin").Should().BeTrue();
        principal.IsInRole("User").Should().BeTrue();
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(TestUser.Id.ToString());
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var tokenString = await CreateService(new List<string> { "User" }).CreateTokenAsync(TestUser);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("a-completely-different-signing-key-of-sufficient-length"))
        };

        var act = () => new JwtSecurityTokenHandler().ValidateToken(tokenString, parameters, out _);

        act.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    [Theory]
    [InlineData(null, Issuer, Audience)]
    [InlineData(Key, null, Audience)]
    [InlineData(Key, Issuer, null)]
    public async Task Missing_jwt_setting_throws_InvalidOperationException(string? key, string? issuer, string? audience)
    {
        var service = CreateService(new List<string>(), new JwtOptions { Key = key, Issuer = issuer, Audience = audience });

        var act = () => service.CreateTokenAsync(TestUser);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
