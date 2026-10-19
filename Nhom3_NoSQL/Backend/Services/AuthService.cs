using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    public class UserSession
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    //Xác thực đăng nhập: mật khẩu băm PBKDF2, phiên lưu trong bộ nhớ (token ngẫu nhiên, hết hạn 8 giờ)
    public class AuthService
    {
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

        private readonly IUserRepository _users;
        private readonly ConcurrentDictionary<string, UserSession> _sessions = new ConcurrentDictionary<string, UserSession>();

        public AuthService(IUserRepository users) => _users = users;

        public static AppUser CreateUser(string email, string fullName, string role, string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return new AppUser
            {
                Email = email.Trim().ToLowerInvariant(),
                FullName = fullName,
                Role = role,
                Salt = Convert.ToBase64String(salt),
                PasswordHash = Hash(password, salt)
            };
        }

        static string Hash(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        public static bool Verify(AppUser user, string password)
        {
            if (user == null || string.IsNullOrEmpty(password)) return false;
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = Convert.FromBase64String(Hash(password, Convert.FromBase64String(user.Salt)));
            // so sánh thời gian hằng để tránh timing attack
            int diff = expected.Length ^ actual.Length;
            for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        public async Task<(string token, UserSession session)> LoginAsync(string email, string password)
        {
            var user = await _users.GetByEmailAsync(email ?? "");
            if (!Verify(user, password)) return (null, null);

            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var session = new UserSession
            {
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                ExpiresAt = DateTime.UtcNow.Add(SessionLifetime)
            };
            _sessions[token] = session;
            return (token, session);
        }

        public UserSession Validate(string token)
        {
            if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token, out var s)) return null;
            if (s.ExpiresAt >= DateTime.UtcNow) return s;
            _sessions.TryRemove(token, out _);
            return null;
        }

        public void Logout(string token)
        {
            if (token != null) _sessions.TryRemove(token, out _);
        }
    }
}
