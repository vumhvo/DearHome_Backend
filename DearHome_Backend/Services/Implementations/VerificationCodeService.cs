using System.Security.Cryptography;
using DearHome_Backend.Exceptions;
using DearHome_Backend.Models;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Services.Interfaces;

namespace DearHome_Backend.Services.Implementations;

/// <summary>
/// Email verification codes used during registration.
/// </summary>
public class VerificationCodeService : IVerificationCodeService
{
    private readonly IVerificationCodeRepository _verificationCodeRepository;
    private readonly IEmailService _emailService;

    public VerificationCodeService(IVerificationCodeRepository verificationCodeRepository, IEmailService emailService)
    {
        _verificationCodeRepository = verificationCodeRepository ?? throw new ArgumentNullException(nameof(verificationCodeRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    public async Task SendAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentNullException(nameof(email), "Email cannot be null or empty.");
        }
        var verificationCode = GenerateVerificationCode();
        var verificationCodeEntity = new VerificationCode
        {
            Email = email,
            Code = verificationCode,
            ExpirationDate = DateTime.UtcNow.AddMinutes(5)
        };
        var existingCode = await _verificationCodeRepository.GetVerificationCodeByEmailAsync(email);
        if (existingCode != null)
        {
            existingCode.Code = verificationCode;
            existingCode.ExpirationDate = DateTime.UtcNow.AddHours(17).AddMinutes(30);
            await _verificationCodeRepository.UpdateAsync(existingCode);
        }
        else
        {
            await _verificationCodeRepository.AddAsync(verificationCodeEntity);
        }

        // Send the verification code to the user's email
        await _emailService.SendVerificationCodeAsync(email, verificationCode);
    }

    public async Task ValidateAsync(string email, string verificationCode)
    {
        var verificationCodeEntity = await _verificationCodeRepository.GetVerificationCodeByEmailAsync(email);
        if (verificationCodeEntity == null || verificationCodeEntity.Code != verificationCode)
        {
            throw new BusinessRuleException("Invalid verification code.");
        }
        // Check if the verification code is expired
        if (verificationCodeEntity.ExpirationDate < DateTime.UtcNow)
        {
            throw new BusinessRuleException("Verification code has expired.");
        }
    }

    private static string GenerateVerificationCode()
    {
        var randomNumber = new byte[6];
        RandomNumberGenerator.Fill(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
