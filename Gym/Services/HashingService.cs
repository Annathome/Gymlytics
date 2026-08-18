using System.Security.Cryptography;

namespace Gym.Services
{
    public class HashingService
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        /// <summary>
        /// Hashes a plain-text password using PBKDF2 with SHA-256 and a secure salt.
        /// </summary>
        public string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            using var algorithm = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                _hashAlgorithm);

            byte[] key = algorithm.GetBytes(KeySize);

            // Return salt and hash combined into a single string
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        /// <summary>
        /// Verifies a plain-text password against a stored combined hash string.
        /// </summary>
        public bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.Contains('.'))
                return false;

            string[] parts = storedHash.Split('.', 2);
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] key = Convert.FromBase64String(parts[1]);

            using var algorithm = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                _hashAlgorithm);

            byte[] keyToCheck = algorithm.GetBytes(KeySize);

            return CryptographicOperations.FixedTimeEquals(key, keyToCheck);
        }
    }
}