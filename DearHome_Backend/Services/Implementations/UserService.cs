using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DearHome_Backend.Constants;
using DearHome_Backend.Exceptions;
using DearHome_Backend.Models;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace DearHome_Backend.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly UserManager<User> _userManager;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPasswordValidator<User> _passwordValidator;
    private readonly ITokenService _tokenService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IVerificationCodeService _verificationCodeService;

    public UserService(
        IUserRepository userRepository,
        IAddressRepository addressRepository,
        UserManager<User> userManager,
        IPasswordHasher<User> passwordHasher,
        IPasswordValidator<User> passwordValidator,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        IVerificationCodeService verificationCodeService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _addressRepository = addressRepository ?? throw new ArgumentNullException(nameof(addressRepository));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _passwordValidator = passwordValidator ?? throw new ArgumentNullException(nameof(passwordValidator));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _googleAuthService = googleAuthService ?? throw new ArgumentNullException(nameof(googleAuthService));
        _verificationCodeService = verificationCodeService ?? throw new ArgumentNullException(nameof(verificationCodeService));
    }

    public async Task<string> LoginAsync(string userName, string password)
    {

        var user = await _userManager.FindByNameAsync(userName);

        if (user == null)
        {
            throw new BusinessRuleException("Invalid username or password.");
        }

        // Check if the password is null or empty
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new BusinessRuleException("Invalid username or password.");
        }

        // Verify the password
        var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (passwordVerificationResult != PasswordVerificationResult.Success)
        {
            throw new BusinessRuleException("Invalid username or password.");
        }

        // Generate JWT token
        return await _tokenService.CreateTokenAsync(user);
    }

    public async Task<string> LoginWithGoogleAsync(string accessToken)
    {
        var userInfo = await _googleAuthService.GetUserInfoAsync(accessToken);

        // Check if the user already exists
        var existingUser = await _userManager.FindByEmailAsync(userInfo.Email);
        if (existingUser != null)
        {
            // Generate JWT token for existing user
            return await _tokenService.CreateTokenAsync(existingUser);
        }

        var user = new User
        {
            Email = userInfo.Email,
            NormalizedEmail = userInfo.Email.ToUpper(),
            UserName = userInfo.Email.Split('@')[0],
            NormalizedUserName = userInfo.Email.Split('@')[0].ToUpper(),
            Name = userInfo.Name,
            EmailConfirmed = true,
            ImageUrl = userInfo.Picture,
            PasswordHash = null, // Password is not needed for Google login
            PhoneNumber = "N/A", // Provide a default value for PhoneNumber
        };

        // Create a new user if it doesn't exist
        var result = await _userManager.CreateAsync(user, "DefaultPassword123!"); // Use a default password or generate one
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("User registration failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Assign roles to the new user
        var roleResult = await _userManager.AddToRoleAsync(user, UserRole.User.ToString());
        if (!roleResult.Succeeded)
        {
            throw new BusinessRuleException("Failed to assign roles: " + string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // Generate JWT token for the new user
        return await _tokenService.CreateTokenAsync(user);
    }

    public async Task<User> RegisterAsync(User user, string password, string verificationCode)
    {

        // Validate the password
        var passwordValidationResult = await _passwordValidator.ValidateAsync(_userManager, user, password);
        if (passwordValidationResult != IdentityResult.Success)
        {
            throw new BusinessRuleException("Password validation failed: " + string.Join(", ", passwordValidationResult.Errors.Select(e => e.Description)));
        }
        // Check if the verification code is valid
        if (string.IsNullOrEmpty(user.Email))
        {
            throw new BusinessRuleException("User email cannot be null or empty.");
        }

        await _verificationCodeService.ValidateAsync(user.Email, verificationCode);

        // Check if the user already exists
        var existingUser = await _userManager.FindByEmailAsync(user.Email);
        if (existingUser != null)
        {
            throw new BusinessRuleException("User already exists.");
        }

        // Save the user to the database
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("User registration failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Assign roles to the user
        var roleResult = await _userManager.AddToRoleAsync(user, UserRole.User.ToString());

        return user;
    }

    public Task SendVerificationCodeAsync(string email) => _verificationCodeService.SendAsync(email);

    public Task<User?> GetUserAsync(Guid id)
    {        
        return _userRepository.GetByIdAsync(id);
    }

    public async Task UpdateAsync(User user)
    {
        var existingUser = await _userRepository.GetByIdWithAddressesAsync(user.Id);

        if (existingUser == null)
        {
            throw new NotFoundException("User not found.");
        }

        //UserName and Email are not allowed to be updated
        user.UserName = existingUser.UserName;
        user.Email = existingUser.Email;
        user.SecurityStamp = existingUser.SecurityStamp;
        user.EmailConfirmed = existingUser.EmailConfirmed;
        user.PasswordHash = existingUser.PasswordHash;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();  

        // Check if the phone number is already in use by another user
        if (user.PhoneNumber != existingUser.PhoneNumber)
        {
            var phoneNumberExists = await _userRepository.IsPhoneNumberExistsAsync(user.PhoneNumber!);
            if (phoneNumberExists)
            {
                throw new BusinessRuleException("Phone number already in use.");
            }
        }

        //Update the user addresses
        if (existingUser.Addresses != null)
        {
            foreach (var address in existingUser.Addresses)
            {
                var updatedAddress = user.Addresses?.FirstOrDefault(a => a.Id == address.Id);
                if (updatedAddress != null)
                {
                    address.Street = updatedAddress.Street;
                    address.Ward = updatedAddress.Ward;
                    address.City = updatedAddress.City;
                    address.District = updatedAddress.District;
                    address.City = updatedAddress.City;
                    address.PostalCode = updatedAddress.PostalCode;
                    address.IsDefault = updatedAddress.IsDefault;
        
                    await _addressRepository.UpdateAsync(address);
                }
                else                {
                    // If the address is not in the updated user, remove it
                    await _addressRepository.DeleteAsync(address);
                }
            }
        }

        //Add new addresses if (user.Addresses != null)
        {
            foreach (var address in user.Addresses!)
            {
                if (address.Id == Guid.Empty)
                {
                    address.UserId = user.Id;
                    await _addressRepository.AddAsync(address);
                }
            }
        }

        await _userRepository.UpdateAsync(user);
    }

    public Task<IEnumerable<User>> GetAllCustomersAsync(int offSet, int limit)
    {
        return _userRepository.GetAllCustomersAsync(offSet, limit);
    }

    public async Task LogoutAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdClaim != null)
        {
            var userId = Guid.Parse(userIdClaim.Value);
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user != null)
            {
                // Invalidate the token by updating the user's security stamp
                user.SecurityStamp = Guid.NewGuid().ToString();
                await _userManager.UpdateAsync(user);
            }
        }
        
    }

    public Task<int> GetTotalCustomersCountAsync()
    {
        return _userRepository.GetTotalCustomersCountAsync();
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string confirmNewPassword)
    {
        // Validate the new password
        if (newPassword != confirmNewPassword)
        {
            throw new BusinessRuleException("New password and confirmation do not match.");
        }

        // Find the user by ID
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        // Check if the password hash is null or empty
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new BusinessRuleException("Cannot verify password. User has no password set.");
        }

        // Verify the current password
        var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (passwordVerificationResult != PasswordVerificationResult.Success)
        {
            throw new BusinessRuleException("Current password is incorrect.");
        }

        // Validate the new password
        var passwordValidationResult = await _passwordValidator.ValidateAsync(_userManager, user, newPassword);
        if (passwordValidationResult != IdentityResult.Success)
        {
            throw new BusinessRuleException("New password validation failed: " + string.Join(", ", passwordValidationResult.Errors.Select(e => e.Description)));
        }

        // Change the password
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _userManager.UpdateAsync(user);
    }
}
