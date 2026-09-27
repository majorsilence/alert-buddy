using System.Security.Cryptography;

namespace AlertBuddy.Core.Security
{
    /// <summary>A PIN as it is stored: a salted hash, never the digits.</summary>
    /// <param name="Hash">Base64 of the PBKDF2 output.</param>
    /// <param name="Salt">Base64 of the random salt.</param>
    public sealed record PinCredential (string Hash, string Salt);

    /// <summary>
    /// The grown-up gate's PIN. It is a gate against a curious child, not security (PLAN.md section 4.3): four digits can be guessed. It is
    /// still never stored in the clear, because a settings file can end up in a backup or a bug report.
    /// </summary>
    public static class PinHasher
    {
        private const int Iterations = 100_000;
        private const int HashBytes = 32;
        private const int SaltBytes = 16;

        /// <summary>A PIN is exactly four digits.</summary>
        public static bool IsValidPin (string? pin) => pin is { Length: 4 } && pin.All (char.IsAsciiDigit);

        /// <summary>Hashes a PIN with a fresh random salt.</summary>
        public static PinCredential Create (string pin)
        {
            if (!IsValidPin (pin))
                throw new ArgumentException ("A PIN is exactly four digits.", nameof (pin));

            var salt = RandomNumberGenerator.GetBytes (SaltBytes);
            return new PinCredential (Convert.ToBase64String (Derive (pin, salt)), Convert.ToBase64String (salt));
        }

        /// <summary>Whether a PIN matches. A wrong format is simply a mismatch.</summary>
        public static bool Verify (string? pin, PinCredential? credential)
        {
            if (credential is null || !IsValidPin (pin))
                return false;

            try {
                var expected = Convert.FromBase64String (credential.Hash);
                var actual = Derive (pin!, Convert.FromBase64String (credential.Salt));

                // Constant time, so how long the comparison takes says nothing about how many digits were right.
                return CryptographicOperations.FixedTimeEquals (expected, actual);
            } catch (FormatException) {
                return false;   // a damaged settings file
            }
        }

        private static byte[] Derive (string pin, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2 (pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
    }
}
