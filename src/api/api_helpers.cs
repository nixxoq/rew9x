using System;
using System.Collections;

using ReW9x;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.Api
{
    public sealed partial class RedditApiClient
    {
        private static string EncodeFeedName(string feedName)
        {
            if (string.IsNullOrEmpty(feedName))
                return AppConfig.DefaultFeed;
            return feedName.Trim();
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
