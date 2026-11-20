using DearHome_Backend.DTOs.UserDtos;

namespace DearHome_Backend.Services.Interfaces;

public interface IGoogleAuthService
{
    /// <summary>
    /// Resolves a Google OAuth access token to the Google profile it belongs to.
    /// Throws <see cref="DearHome_Backend.Exceptions.BusinessRuleException"/> when the token is invalid.
    /// </summary>
    Task<GoogleUserInfo> GetUserInfoAsync(string accessToken);
}
