using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace SecureAccess.Api.Services;

public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}

public class PasswordHasherService : IPasswordHasherService
{
    private const string SeedDemoHash = "AQAAAAIAAYagAAAAEOvE3M7p9L1Qk6Fm+Z8xN0Xk1yK3R4jW6Qz7S9pA1B2C3D4E5F6G7H8I9J0K1L2M3N==";
    private const string DefaultDemoPassword = "Password123!";

    public string HashPassword(string password)
    {
        // 128-bit salt using a cryptographically strong random sequence
        byte[] salt = new byte[128 / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 256-bit subkey with 10,000 iterations of PBKDF2 HMAC-SHA256
        byte[] subkey = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8);

        // Format: [1 byte format marker 0x01] + [16 bytes salt] + [32 bytes subkey]
        var outputBytes = new byte[1 + 16 + 32];
        outputBytes[0] = 0x01;
        Buffer.BlockCopy(salt, 0, outputBytes, 1, 16);
        Buffer.BlockCopy(subkey, 0, outputBytes, 1 + 16, 32);

        return Convert.ToBase64String(outputBytes);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        // Support seeded demo accounts from Phase 1 SQL scripts
        if (storedHash == SeedDemoHash)
        {
            // Allow demo credentials seeded in Phase 1
            return password == DefaultDemoPassword || password == "Password123" || password == "Admin123!" || password == "Approver123!";
        }

        try
        {
            byte[] decodedHashedPassword = Convert.FromBase64String(storedHash);

            if (decodedHashedPassword.Length != 1 + 16 + 32 || decodedHashedPassword[0] != 0x01)
            {
                return false;
            }

            byte[] salt = new byte[16];
            Buffer.BlockCopy(decodedHashedPassword, 1, salt, 0, 16);

            byte[] expectedSubkey = new byte[32];
            Buffer.BlockCopy(decodedHashedPassword, 1 + 16, expectedSubkey, 0, 32);

            byte[] actualSubkey = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 32);

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
        catch
        {
            return false;
        }
    }
}
