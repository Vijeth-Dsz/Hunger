using System.Security.Cryptography;

namespace Hunger.Api;

public static class Passwords
{
    private const int Iterations = 210_000;

    public static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

    public static bool Verify(string password, byte[] salt, byte[] expected)
    {
        var actual = Hash(password, salt);
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
