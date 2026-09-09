using System.Security.Cryptography;

namespace ISP.Ticketing.Infrastructure.Services;

public static class PasswordService
{
    private const int Iterations = 120_000;
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string encoded)
    {
        var p = encoded.Split(':');
        if (p.Length != 4 || p[0] != "PBKDF2-SHA256" || !int.TryParse(p[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(p[2]); var expected = Convert.FromBase64String(p[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
