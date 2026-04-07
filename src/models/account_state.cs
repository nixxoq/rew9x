using System;

namespace ReW9x.Models
{
    public sealed class AccountState
    {
        public string AccessToken;
        public string RefreshToken;
        public string TokenType;
        public string Scope;
        public long ExpiresAtUtcTicks;
        public string RedditUsername;
        public string SelectedFeed;
        public string LastSubreddit;
        public long LastLoginUtcTicks;

        public RedditToken ToToken()
        {
            if (string.IsNullOrEmpty(AccessToken) && string.IsNullOrEmpty(RefreshToken))
                return null;

            RedditToken t = new RedditToken();
            t.AccessToken = AccessToken;
            t.RefreshToken = RefreshToken;
            t.TokenType = TokenType;
            t.Scope = Scope;

            if (ExpiresAtUtcTicks > 0)
                t.ExpiresAtUtc = new DateTime(ExpiresAtUtcTicks, DateTimeKind.Utc);
            else
                t.ExpiresAtUtc = DateTime.MinValue;

            return t;
        }

        public static AccountState FromToken(RedditToken token)
        {
            if (token == null)
                return null;

            AccountState s = new AccountState();
            s.AccessToken = token.AccessToken;
            s.RefreshToken = token.RefreshToken;
            s.TokenType = token.TokenType;
            s.Scope = token.Scope;
            s.ExpiresAtUtcTicks = token.ExpiresAtUtc.Ticks;
            s.LastLoginUtcTicks = DateTime.UtcNow.Ticks;
            return s;
        }
    }
}
