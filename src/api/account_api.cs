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
        public RedditUser GetCurrentUser()
        {
            EnsureValidToken();

            string json = RequestJson("GET", "https://oauth.reddit.com/api/v1/me", null, null, true);
            object root = JsonParser.Parse(json);
            Hashtable obj = JsonParser.AsObject(root);
            if (obj == null)
                throw new InvalidOperationException("Invalid me response.");

            RedditUser u = new RedditUser();
            u.Name = JsonParser.GetString(obj, "name");
            u.Id = JsonParser.GetString(obj, "id");
            u.IconImg = JsonParser.GetString(obj, "icon_img");
            u.LinkKarma = JsonParser.GetInt(obj, "link_karma");
            u.CommentKarma = JsonParser.GetInt(obj, "comment_karma");
            u.CreatedUtc = JsonParser.GetDouble(obj, "created_utc");
            return u;
        }

        public List<RedditSubreddit> LoadSubscribedSubreddits(int maxCount)
        {
            List<RedditSubreddit> result =
            new List<RedditSubreddit>();

            if (!IsAuthenticated)
                return result;

            if (maxCount <= 0)
                maxCount = 100;

            string after = null;

            while (result.Count < maxCount)
            {
                int batchLimit = maxCount - result.Count;

                if (batchLimit > 100)
                    batchLimit = 100;

                string path =
                "/subreddits/mine/subscriber.json?raw_json=1&limit=" +
                batchLimit.ToString(CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(after))
                    path += "&after=" + Uri.EscapeDataString(after);

                string url =
                BuildApiUrl(path);

                string json =
                RequestJson("GET", url, null, null, true);

                object root =
                JsonParser.Parse(json);

                Hashtable obj =
                JsonParser.AsObject(root);

                if (obj == null)
                    break;

                Hashtable data =
                JsonParser.GetObject(obj, "data");

                if (data == null)
                    break;

                after =
                JsonParser.GetString(data, "after");

                ArrayList children =
                JsonParser.GetArray(data, "children");

                if (children == null ||
                    children.Count == 0)
                    break;

                int i;
                for (i = 0;
                     i < children.Count &&
                     result.Count < maxCount;
                     i++)
                {
                    RedditSubreddit subreddit =
                    ParseSubreddit(
                        JsonParser.AsObject(children[i]));

                    if (subreddit != null)
                        result.Add(subreddit);
                }

                if (string.IsNullOrEmpty(after))
                    break;
            }

            return result;
        }
    }
}
