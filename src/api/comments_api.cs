using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ReW9x;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.Api
{
    public sealed partial class RedditApiClient
    {
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
    }
}
