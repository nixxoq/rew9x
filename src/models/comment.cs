using System.Collections.Generic;

namespace ReW9x.Models
{
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
}
