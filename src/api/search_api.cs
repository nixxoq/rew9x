using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using ReW9x;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.Api
{
    public sealed partial class RedditApiClient
    {
        public RedditListingPage SearchPosts(string query, string after, int limit)
        {
            EnsureValidToken();

            if (string.IsNullOrEmpty(query))
                throw new InvalidOperationException("Search query is empty.");

            if (limit <= 0)
                limit = 10;

            string path =
            "/search.json?raw_json=1&limit=" +
            limit.ToString(CultureInfo.InvariantCulture) +
            "&q=" +
            Uri.EscapeDataString(query);

            if (!string.IsNullOrEmpty(after))
                path += "&after=" + Uri.EscapeDataString(after);

            string url =
            BuildApiUrl(path);

            string json =
            RequestJson("GET", url, null, null, false);

            object root =
            JsonParser.Parse(json);

            Hashtable obj =
            JsonParser.AsObject(root);

            if (obj == null)
                throw new InvalidOperationException("Invalid search response.");

            Hashtable data =
            JsonParser.GetObject(obj, "data");

            if (data == null)
                throw new InvalidOperationException("Invalid search response.");

            RedditListingPage page =
            new RedditListingPage();

            page.After =
            JsonParser.GetString(data, "after");

            page.Before =
            JsonParser.GetString(data, "before");

            ArrayList children =
            JsonParser.GetArray(data, "children");

            if (children != null)
            {
                int i;
                for (i = 0;
                     i < children.Count;
                     i++)
                {
                    RedditPost post =
                    ParsePost(
                        JsonParser.AsObject(children[i]));

                    if (post != null)
                        page.Posts.Add(post);
                }
            }

            return page;
        }

        public List<RedditSubreddit> SearchSubreddits(string query, int limit)
        {
            EnsureValidToken();

            List<RedditSubreddit> result =
            new List<RedditSubreddit>();

            if (string.IsNullOrEmpty(query))
                return result;

            if (limit <= 0)
                limit = 10;

            string path =
            "/subreddits/search.json?raw_json=1&limit=" +
            limit.ToString(CultureInfo.InvariantCulture) +
            "&q=" +
            Uri.EscapeDataString(query);

            string url =
            BuildApiUrl(path);

            string json =
            RequestJson("GET", url, null, null, false);

            object root =
            JsonParser.Parse(json);

            Hashtable obj =
            JsonParser.AsObject(root);

            if (obj == null)
                return result;

            Hashtable data =
            JsonParser.GetObject(obj, "data");

            if (data == null)
                return result;

            ArrayList children =
            JsonParser.GetArray(data, "children");

            if (children == null)
                return result;

            int i;
            for (i = 0;
                 i < children.Count;
                 i++)
            {
                RedditSubreddit subreddit =
                ParseSubreddit(
                    JsonParser.AsObject(children[i]));

                if (subreddit != null)
                    result.Add(subreddit);
            }

            return result;
        }
    }
}
