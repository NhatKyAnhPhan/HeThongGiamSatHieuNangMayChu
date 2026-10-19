using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    public static class UserRole
    {
        public const string Admin = "Admin";   // toàn quyền: sửa host, ngưỡng, xử lý sự cố
        public const string Viewer = "Viewer"; // chỉ xem
    }

    [BsonIgnoreExtraElements]
    public class AppUser
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Email { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } = UserRole.Viewer;
        public string PasswordHash { get; set; } // PBKDF2, base64
        public string Salt { get; set; }
    }
}
