using System;
using System.Collections;
using System.Globalization;

using ReW9x;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.Api
{
    public sealed partial class RedditApiClient
    {
        public RedditListingPage LoadFeed(string feedName, string after, int limit)
        {
            EnsureValidToken();

            if (string.IsNullOrEmpty(feedName))
                feedName = AppConfig.DefaultFeed;

            if (feedName.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            {
                string query =
                feedName.Substring(7);

                return SearchPosts(
                    query,
                    after,
                    limit);
            }

            string path = "/" + EncodeFeedName(feedName) + ".json?raw_json=1&limit=" + limit.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(after))
                path += "&after=" + Uri.EscapeDataString(after);

            string url = BuildApiUrl(path);
            string json = RequestJson("GET", url, null, null, false);

            object root = JsonParser.Parse(json);
            Hashtable obj = JsonParser.AsObject(root);
            if (obj == null)
                throw new InvalidOperationException("Invalid listing response.");

            Hashtable data = JsonParser.GetObject(obj, "data");
            if (data == null)
                throw new InvalidOperationException("Invalid listing response.");

            RedditListingPage page = new RedditListingPage();
            page.After = JsonParser.GetString(data, "after");
            page.Before = JsonParser.GetString(data, "before");

            ArrayList children = JsonParser.GetArray(data, "children");
            if (children != null)
            {
                int i;
                for (i = 0; i < children.Count; i++)
                {
                    RedditPost post = ParsePost(JsonParser.AsObject(children[i]));
                    if (post != null)
                        page.Posts.Add(post);
                }
            }

            return page;
        }

        private static RedditPost ParsePostListingFirst(object listingRoot)
        {
            Hashtable listing = JsonParser.AsObject(listingRoot);
            if (listing == null) return null;

            Hashtable data = JsonParser.GetObject(listing, "data");
            if (data == null) return null;

            ArrayList children = JsonParser.GetArray(data, "children");
            if (children == null || children.Count == 0)
                return null;

            return ParsePost(JsonParser.AsObject(children[0]));
        }

        private static RedditPost ParsePost(Hashtable child)
        {
            if (child == null) return null;

            Hashtable data = JsonParser.GetObject(child, "data");
            if (data == null) return null;

            RedditPost p = new RedditPost();
            p.Id = JsonParser.GetString(data, "id");
            p.FullName = JsonParser.GetString(data, "name");
            p.Title = JsonParser.GetString(data, "title");
            p.Subreddit = JsonParser.GetString(data, "subreddit");
            p.Author = JsonParser.GetString(data, "author");
            p.Permalink = JsonParser.GetString(data, "permalink");
            p.Url = JsonParser.GetString(data, "url");
            p.SelfText = JsonParser.GetString(data, "selftext");
            p.Domain = JsonParser.GetString(data, "domain");
            p.PostHint = JsonParser.GetString(data, "post_hint");
            p.Thumbnail = JsonParser.GetString(data, "thumbnail");
            p.PreviewImageUrl = GetPreviewImageUrl(data);
            p.PreviewImageWidth = GetPreviewImageWidth(data);
            p.PreviewImageHeight = GetPreviewImageHeight(data);
            p.IsSelf = JsonParser.GetBool(data, "is_self");
            p.IsNsfw = JsonParser.GetBool(data, "over_18");
            p.Score = JsonParser.GetInt(data, "score");
            p.NumComments = JsonParser.GetInt(data, "num_comments");
            p.CreatedUtc = JsonParser.GetDouble(data, "created_utc");
            return p;
        }
    }
}
