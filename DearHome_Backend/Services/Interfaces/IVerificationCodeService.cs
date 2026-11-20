namespace DearHome_Backend.Services.Interfaces;

public interface IVerificationCodeService
{
    /// <summary>Generates (or regenerates) the email's verification code and emails it.</summary>
    Task SendAsync(string email);

    /// <summary>
    /// Checks the code against the one stored for the email.
    /// Throws <see cref="DearHome_Backend.Exceptions.BusinessRuleException"/> when it is wrong or expired.
    /// </summary>
    Task ValidateAsync(string email, string verificationCode);
}
