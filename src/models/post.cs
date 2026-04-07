namespace ReW9x.Models
{
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
}
