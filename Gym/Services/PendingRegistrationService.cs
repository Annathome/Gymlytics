using Microsoft.Extensions.Caching.Memory;
using System;
using System.Security.Cryptography;

namespace Gym.Services
{
    public class PendingRegistrationService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _defaultLifetime = TimeSpan.FromMinutes(30);

        public PendingRegistrationService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public string CreateToken(string payload, TimeSpan? lifetime = null)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentException("Payload cannot be null or empty.", nameof(payload));

            string token = GenerateSecureToken();
            TimeSpan duration = lifetime ?? _defaultLifetime;

            _cache.Set(token, payload, duration);

            return token;
        }

        /// <summary>
        /// Checks if a token exists and is valid WITHOUT removing it from cache.
        /// Useful for GET requests rendering forms.
        /// </summary>
        public bool ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return _cache.TryGetValue(token, out _);
        }

        /// <summary>
        /// Consumes a token. Removes it from cache to prevent reuse.
        /// </summary>
        public (bool Success, string? Payload) Consume(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, null);

            if (_cache.TryGetValue(token, out string? payload))
            {
                _cache.Remove(token);
                return (true, payload);
            }

            return (false, null);
        }

        private static string GenerateSecureToken()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}