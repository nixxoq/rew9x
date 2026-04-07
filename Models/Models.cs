using System;
using System.Collections.Generic;

namespace Reddit98Client
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

    public sealed class RedditUser
    {
        public string Name;
        public string Id;
        public string IconImg;
        public int LinkKarma;
        public int CommentKarma;
        public double CreatedUtc;
    }

    public sealed class RedditPost
    {
        public string Id;
        public string FullName;
        public string Title;
        public string Subreddit;
        public string Author;
        public string Permalink;
        public string Url;
        public string SelfText;
        public string Domain;
        public string PostHint;
        public string Thumbnail;
        public string PreviewImageUrl;
        public int PreviewImageWidth;
        public int PreviewImageHeight;
        public bool IsSelf;
        public bool IsNsfw;
        public int Score;
        public int NumComments;
        public double CreatedUtc;
    }

    public sealed class RedditSubreddit
    {
        public string Id;
        public string Name;
        public string DisplayName;
        public string DisplayNamePrefixed;
        public string Title;
        public string Url;
        public bool UserIsSubscriber;
    }

    public sealed class RedditComment
    {
        public string Id;
        public string FullName;
        public string ParentFullName;
        public string Author;
        public string Body;
        public string BodyHtml;
        public string Permalink;
        public int Score;
        public int Depth;
        public double CreatedUtc;
        public bool IsMore;
        public List<string> MoreChildrenIds = new List<string>();
        public List<RedditComment> Replies = new List<RedditComment>();
    }

    public sealed class RedditListingPage
    {
        public List<RedditPost> Posts = new List<RedditPost>();
        public string After;
        public string Before;
    }

    public sealed class RedditThread
    {
        public RedditPost Post;
        public List<RedditComment> Comments = new List<RedditComment>();
    }

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
