using DearHome_Backend.DTOs.UserDtos;
using DearHome_Backend.Exceptions;
using DearHome_Backend.Services.Interfaces;

namespace DearHome_Backend.Services.Implementations;

/// <summary>
/// Verifies Google access tokens against the Google userinfo endpoint. Registered as a typed HttpClient.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v1/userinfo";

    private readonly HttpClient _httpClient;

    public GoogleAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<GoogleUserInfo> GetUserInfoAsync(string accessToken)
    {
        using var response = await _httpClient.GetAsync($"{UserInfoEndpoint}?access_token={accessToken}");
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleException("Invalid Google access token.");
        }

        var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfo>();
        if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
        {
            throw new BusinessRuleException("Failed to retrieve user information from Google.");
        }

        return userInfo;
    }
}
