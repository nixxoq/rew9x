namespace ReW9x
{
    public static class AppConfig
    {
        public const string ClientId = "PUT_YOUR_REDDIT_CLIENT_ID";
        public const string RedirectUri = "http://127.0.0.1:65010/reddit98-callback";
        public const string Scope = "identity read history save";
        public const string UserAgent = "reW9x/0.5 (Win9x; .NET 2.0)";
        public const string AccountFileName = "account.json";
        public const string DefaultFeed = "best";
        public const int FeedPageSize = 25;
        public const int CommentPageLimit = 500;
        public const int MoreChildrenLimit = 100;
    }
}
