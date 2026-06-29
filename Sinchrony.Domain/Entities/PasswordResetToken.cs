using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Entities
{
    public class PasswordResetToken
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; }
        public string Token { get; private set; } = string.Empty;
        public DateTime ExpiresAt { get; private set; }
        public bool Used { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        public User? User { get; private set; }

        protected PasswordResetToken() { }

        public static PasswordResetToken Create(Guid userId, int expiryMinutes = 60)
            => new()
            {
                UserId = userId,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };

        public bool IsValid() => !Used && ExpiresAt > DateTime.UtcNow;
        public void MarkUsed() => Used = true;
    }
}
