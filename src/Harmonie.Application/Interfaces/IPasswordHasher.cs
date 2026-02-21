namespace Harmonie.Application.Interfaces;

/// <summary>
/// Interface for password hashing operations.
/// Implementation should use strong algorithms like BCrypt or PBKDF2.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash a plain text password
    /// </summary>
    /// <param name="email">Email of the user</param>
    /// <param name="password">Plain text password</param>
    /// <returns>Hashed password</returns>
    string HashPassword(string email, string password);

    /// <summary>
    /// Verify a password against a hash
    /// </summary>
    /// <param name="email">Email of the user</param>
    /// <param name="hashedPassword">The hashed password</param>
    /// <param name="providedPassword">The password to verify</param>
    /// <returns>True if password matches, false otherwise</returns>
    bool VerifyPassword(string email, string hashedPassword, string providedPassword);
}
