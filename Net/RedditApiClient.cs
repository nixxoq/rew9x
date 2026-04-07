using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Reddit98Client
{
    public sealed class RedditApiClient
    {
        private readonly OAuthClient _oauth;
        private RedditToken _token;
        private bool _refreshing;

        public event EventHandler TokenChanged;

        public RedditApiClient(OAuthClient oauth, RedditToken token)
        {
            _oauth = oauth;
            _token = token;
        }

        public RedditToken CurrentToken
        {
            get { return _token; }
            set
            {
                _token = value;
                OnTokenChanged();
            }
        }

        public bool IsAuthenticated
        {
            get { return _token != null && _token.HasAccessToken; }
        }

        public void EnsureValidToken()
        {
            if (_token == null)
                return;

            if (!_token.IsExpiredSoon)
                return;

            if (!_token.HasRefreshToken || _refreshing)
                return;

            try
            {
                _refreshing = true;
                RedditToken refreshed = _oauth.RefreshToken(_token.RefreshToken);
                if (refreshed != null && refreshed.HasAccessToken)
                {
                    if (string.IsNullOrEmpty(refreshed.RefreshToken))
                        refreshed.RefreshToken = _token.RefreshToken;
                    _token = refreshed;
                    OnTokenChanged();
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

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

        public RedditThread LoadComments(string permalink, string sort, int limit, int depth)
        {
            EnsureValidToken();

            if (string.IsNullOrEmpty(sort))
                sort = "confidence";

            string path = permalink;
            if (!path.StartsWith("/"))
                path = "/" + path;
            path += ".json?raw_json=1&sort=" + Uri.EscapeDataString(sort);
            path += "&limit=" + limit.ToString(CultureInfo.InvariantCulture);
            path += "&depth=" + depth.ToString(CultureInfo.InvariantCulture);

            string url = BuildApiUrl(path);
            string json = RequestJson("GET", url, null, null, false);

            object root = JsonParser.Parse(json);
            ArrayList arr = JsonParser.AsArray(root);
            if (arr == null || arr.Count < 2)
                throw new InvalidOperationException("Invalid comment response.");

            RedditThread thread = new RedditThread();
            thread.Post = ParsePostListingFirst(arr[0]);
            thread.Comments = ParseCommentListing(arr[1]);

            return thread;
        }

        public List<RedditComment> LoadMoreChildren(string linkFullName, string[] children, string sort)
        {
            EnsureValidToken();

            if (children == null || children.Length == 0)
                return new List<RedditComment>();

            if (string.IsNullOrEmpty(sort))
                sort = "confidence";

            StringBuilder sb = new StringBuilder();
            sb.Append("/api/morechildren.json?api_type=json&raw_json=1");
            sb.Append("&link_id=");
            sb.Append(Uri.EscapeDataString(linkFullName));
            sb.Append("&children=");
            int i;
            for (i = 0; i < children.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Uri.EscapeDataString(children[i]));
            }
            sb.Append("&sort=");
            sb.Append(Uri.EscapeDataString(sort));
            sb.Append("&limit_children=true");

            string url = BuildApiUrl(sb.ToString());
            string json = RequestJson("GET", url, null, null, true);

            object root = JsonParser.Parse(json);
            Hashtable obj = JsonParser.AsObject(root);
            if (obj == null)
                return new List<RedditComment>();

            object payload = obj;
            if (obj.ContainsKey("json"))
                payload = obj["json"];

            Hashtable payloadObj = JsonParser.AsObject(payload);
            if (payloadObj == null)
                return new List<RedditComment>();

            Hashtable data = JsonParser.GetObject(payloadObj, "data");
            if (data == null)
                return new List<RedditComment>();

            ArrayList things = JsonParser.GetArray(data, "things");
            if (things == null)
                things = JsonParser.GetArray(data, "children");

            List<RedditComment> comments = new List<RedditComment>();
            if (things != null)
            {
                for (i = 0; i < things.Count; i++)
                {
                    RedditComment c = ParseCommentNode(JsonParser.AsObject(things[i]));
                    if (c != null)
                        comments.Add(c);
                }
            }

            return comments;
        }

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

        public byte[] DownloadBytes(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int attempt;
            for (attempt = 0; attempt < 2; attempt++)
            {
                EnsureValidToken();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = _oauth.UserAgentValue;
                headers["Accept"] = "image/*,*/*;q=0.8";
                headers["Accept-Language"] = "en-US,en;q=0.8";

                if (IsAuthenticated && _token != null && !string.IsNullOrEmpty(_token.AccessToken))
                    headers["Authorization"] = "bearer " + _token.AccessToken;

                Win98TlsClient.HttpResponse resp =
                Win98TlsClient.HttpsClient.Get(url, headers);

                if (resp.StatusCode == 401 &&
                    _token != null &&
                    _token.HasRefreshToken &&
                    attempt == 0)
                {
                    _token = _oauth.RefreshToken(_token.RefreshToken);
                    OnTokenChanged();
                    continue;
                }

                if (resp.StatusCode < 200 || resp.StatusCode >= 300)
                    throw new InvalidOperationException("HTTP " + resp.StatusCode.ToString(CultureInfo.InvariantCulture));

                return resp.Body;
            }

            throw new InvalidOperationException("Request failed.");
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

        private string BuildApiUrl(string path)
        {
            if (IsAuthenticated)
                return "https://oauth.reddit.com" + path;
            return "https://www.reddit.com" + path;
        }

        private string RequestJson(string method, string url, string contentType, byte[] body, bool preferAuth)
        {
            int attempt;
            for (attempt = 0; attempt < 2; attempt++)
            {
                EnsureValidToken();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = _oauth.UserAgentValue;
                headers["Accept"] = "application/json";
                headers["Accept-Language"] = "en-US,en;q=0.8";

                if (IsAuthenticated && _token != null && !string.IsNullOrEmpty(_token.AccessToken))
                    headers["Authorization"] = "bearer " + _token.AccessToken;

                if (body != null && contentType != null)
                    headers["Content-Type"] = contentType;

                Win98TlsClient.HttpResponse resp;
                if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    resp = Win98TlsClient.HttpsClient.Get(url, headers);
                else
                    resp = Win98TlsClient.HttpsClient.Request(method, url, headers, contentType, body);

                if (resp.StatusCode == 401 && _token != null && _token.HasRefreshToken && attempt == 0)
                {
                    _token = _oauth.RefreshToken(_token.RefreshToken);
                    OnTokenChanged();
                    continue;
                }

                if (resp.StatusCode < 200 || resp.StatusCode >= 300)
                    throw new InvalidOperationException("HTTP " + resp.StatusCode.ToString(CultureInfo.InvariantCulture));

                return resp.Text;
            }

            throw new InvalidOperationException("Request failed.");
        }

        private static string EncodeFeedName(string feedName)
        {
            if (string.IsNullOrEmpty(feedName))
                return AppConfig.DefaultFeed;
            return feedName.Trim();
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

        private static List<RedditComment> ParseCommentListing(object listingRoot)
        {
            List<RedditComment> list = new List<RedditComment>();
            Hashtable listing = JsonParser.AsObject(listingRoot);
            if (listing == null) return list;

            Hashtable data = JsonParser.GetObject(listing, "data");
            if (data == null) return list;

            ArrayList children = JsonParser.GetArray(data, "children");
            if (children == null) return list;

            int i;
            for (i = 0; i < children.Count; i++)
            {
                RedditComment c = ParseCommentNode(JsonParser.AsObject(children[i]));
                if (c != null)
                    list.Add(c);
            }

            return list;
        }

        private static RedditSubreddit ParseSubreddit(
            Hashtable child)
        {
            if (child == null)
                return null;

            Hashtable data =
            JsonParser.GetObject(child, "data");

            if (data == null)
                return null;

            RedditSubreddit subreddit =
            new RedditSubreddit();

            subreddit.Id =
            JsonParser.GetString(data, "id");

            subreddit.Name =
            JsonParser.GetString(data, "name");

            subreddit.DisplayName =
            JsonParser.GetString(data, "display_name");

            subreddit.DisplayNamePrefixed =
            JsonParser.GetString(data, "display_name_prefixed");

            subreddit.Title =
            JsonParser.GetString(data, "title");

            subreddit.Url =
            JsonParser.GetString(data, "url");

            subreddit.UserIsSubscriber =
            JsonParser.GetBool(data, "user_is_subscriber");

            return subreddit;
        }

        private static RedditComment ParseCommentNode(Hashtable node)
        {
            if (node == null) return null;

            string kind = JsonParser.GetString(node, "kind");
            Hashtable data = JsonParser.GetObject(node, "data");
            if (data == null) return null;

            RedditComment c = new RedditComment();
            c.Id = JsonParser.GetString(data, "id");
            c.FullName = JsonParser.GetString(data, "name");
            c.ParentFullName = JsonParser.GetString(data, "parent_id");
            c.Author = JsonParser.GetString(data, "author");
            c.Body = JsonParser.GetString(data, "body");
            c.BodyHtml = JsonParser.GetString(data, "body_html");
            c.Permalink = JsonParser.GetString(data, "permalink");
            c.Score = JsonParser.GetInt(data, "score");
            c.Depth = JsonParser.GetInt(data, "depth");
            c.CreatedUtc = JsonParser.GetDouble(data, "created_utc");

            if (string.Equals(kind, "more", StringComparison.OrdinalIgnoreCase))
            {
                c.IsMore = true;
                ArrayList children = JsonParser.GetArray(data, "children");
                if (children != null)
                {
                    int i;
                    for (i = 0; i < children.Count; i++)
                    {
                        string childId = JsonParser.AsString(children[i]);
                        if (!string.IsNullOrEmpty(childId))
                            c.MoreChildrenIds.Add(childId);
                    }
                }
                return c;
            }

            object replies = data["replies"];
            if (replies != null && !(replies is string))
            {
                Hashtable repliesObj = JsonParser.AsObject(replies);
                if (repliesObj != null)
                {
                    Hashtable repliesData = JsonParser.GetObject(repliesObj, "data");
                    if (repliesData != null)
                    {
                        ArrayList replyChildren = JsonParser.GetArray(repliesData, "children");
                        if (replyChildren != null)
                        {
                            int j;
                            for (j = 0; j < replyChildren.Count; j++)
                            {
                                RedditComment child = ParseCommentNode(JsonParser.AsObject(replyChildren[j]));
                                if (child != null)
                                    c.Replies.Add(child);
                            }
                        }
                    }
                }
            }

            return c;
        }

        private void OnTokenChanged()
        {
            if (TokenChanged != null)
                TokenChanged(this, EventArgs.Empty);
        }

        private static string GetPreviewImageUrl(
            Hashtable data)
        {
            Hashtable preview =
            JsonParser.GetObject(data, "preview");

            if (preview == null)
                return null;

            ArrayList images =
            JsonParser.GetArray(preview, "images");

            if (images == null || images.Count == 0)
                return null;

            Hashtable image =
            JsonParser.AsObject(images[0]);

            if (image == null)
                return null;

            Hashtable source =
            JsonParser.GetObject(image, "source");

            if (source == null)
                return null;

            return DecodeHtml(
                JsonParser.GetString(source, "url"));
        }

        private static int GetPreviewImageWidth(
            Hashtable data)
        {
            Hashtable source =
            GetPreviewSource(data);

            if (source == null)
                return 0;

            return JsonParser.GetInt(source, "width");
        }

        private static int GetPreviewImageHeight(
            Hashtable data)
        {
            Hashtable source =
            GetPreviewSource(data);

            if (source == null)
                return 0;

            return JsonParser.GetInt(source, "height");
        }

        private static Hashtable GetPreviewSource(
            Hashtable data)
        {
            Hashtable preview =
            JsonParser.GetObject(data, "preview");

            if (preview == null)
                return null;

            ArrayList images =
            JsonParser.GetArray(preview, "images");

            if (images == null || images.Count == 0)
                return null;

            Hashtable image =
            JsonParser.AsObject(images[0]);

            if (image == null)
                return null;

            return JsonParser.GetObject(image, "source");
        }

        private static string DecodeHtml(
            string text)
        {
            return HtmlUtil.DecodeBasic(text);
        }
    }
}
