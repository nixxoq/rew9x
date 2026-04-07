using System.Collections.Generic;

namespace ReW9x.Models
{
    public sealed class RedditThread
    {
        public RedditPost Post;
        public List<RedditComment> Comments = new List<RedditComment>();
    }
}
