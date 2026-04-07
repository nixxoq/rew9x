namespace Reddit98Client
{
    public static class AppConfig
    {
        public const string ClientId = "PUT_YOUR_REDDIT_CLIENT_ID_HERE";
        public const string RedirectUri = "http://127.0.0.1:65010/reddit98-callback";
        public const string Scope = "identity read history save";
        public const string UserAgent = "reddit98client/0.1 (Win98; .NET 2.0)";
        public const string AccountFileName = "account.json";
        public const string DefaultFeed = "best";
        public const int FeedPageSize = 25;
        public const int CommentPageLimit = 500;
        public const int MoreChildrenLimit = 100;
    }
}
