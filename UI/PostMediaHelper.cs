using System.Drawing;
using System.IO;

namespace Reddit98Client
{
    public static class PostMediaHelper
    {
        public static string GetPreferredImageUrl(
            RedditPost post)
        {
            if (post == null)
                return null;

            if (IsDirectImageUrl(post.Url))
                return post.Url;

            if (!string.IsNullOrEmpty(
                post.PreviewImageUrl))
                return post.PreviewImageUrl;

            if (IsDirectImageUrl(
                post.Thumbnail))
                return post.Thumbnail;

            return null;
        }

        public static bool IsDirectImageUrl(
            string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            string lower =
            url.ToLowerInvariant();

            return lower.EndsWith(".jpg") ||
                   lower.EndsWith(".jpeg") ||
                   lower.EndsWith(".png") ||
                   lower.EndsWith(".gif") ||
                   lower.EndsWith(".bmp") ||
                   lower.IndexOf("i.redd.it/") >= 0 ||
                   lower.IndexOf("preview.redd.it/") >= 0;
        }

        public static Image BuildDetachedImage(
            byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            MemoryStream stream =
            new MemoryStream(bytes);

            Image temp =
            Image.FromStream(stream);

            Bitmap copy =
            new Bitmap(temp);

            temp.Dispose();
            stream.Close();

            return copy;
        }
    }
}
