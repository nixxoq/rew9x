using System;

namespace ReW9x.Models
{
    public sealed class RedditToken
    {
        public string AccessToken;
        public string RefreshToken;
        public string TokenType;
        public string Scope;
        public DateTime ExpiresAtUtc;

        public bool HasAccessToken
        {
            get { return !string.IsNullOrEmpty(AccessToken); }
        }

        public bool HasRefreshToken
        {
            get { return !string.IsNullOrEmpty(RefreshToken); }
        }

        public bool IsExpiredSoon
        {
            get { return DateTime.UtcNow.AddMinutes(2) >= ExpiresAtUtc; }
        }
    }
}
