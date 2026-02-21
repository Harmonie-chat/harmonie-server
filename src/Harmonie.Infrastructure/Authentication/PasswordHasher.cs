using System.Security.Cryptography;
using System.Text;
using Harmonie.Application.Interfaces;

namespace Harmonie.Infrastructure.Authentication;
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 300_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string email, string password) 
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var userBytes = Encoding.UTF8.GetBytes(email);
        var saltWithUser = new byte[salt.Length + userBytes.Length];
        salt.CopyTo(saltWithUser, 0);
        userBytes.CopyTo(saltWithUser, salt.Length);

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltWithUser, Iterations, Algorithm, HashSize);

        // Format: salt (16) + hash (32)
        var result = new byte[SaltSize + HashSize];
        salt.CopyTo(result, 0);
        hash.CopyTo(result, SaltSize);

        return Convert.ToBase64String(result);
    }
    public bool VerifyPassword(string email, string hashedPassword, string providedPassword)
    {
        var decoded = Convert.FromBase64String(hashedPassword);
        if (decoded.Length != SaltSize + HashSize) return false;

        var salt = decoded[..SaltSize];
        var expectedHash = decoded[SaltSize..];

        var userBytes = Encoding.UTF8.GetBytes(email);
        var saltWithUser = new byte[salt.Length + userBytes.Length];
        salt.CopyTo(saltWithUser, 0);
        userBytes.CopyTo(saltWithUser, salt.Length);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(providedPassword, saltWithUser, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
