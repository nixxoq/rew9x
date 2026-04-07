using System.Collections.Generic;

namespace ReW9x.Models
{
    public sealed class RedditListingPage
    {
        public List<RedditPost> Posts = new List<RedditPost>();
        public string After;
        public string Before;
    }
}
