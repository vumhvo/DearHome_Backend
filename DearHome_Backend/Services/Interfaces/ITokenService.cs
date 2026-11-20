using DearHome_Backend.Models;

namespace DearHome_Backend.Services.Interfaces;

public interface ITokenService
{
    /// <summary>Creates a signed JWT for the user, with one role claim per assigned role.</summary>
    Task<string> CreateTokenAsync(User user);
}
