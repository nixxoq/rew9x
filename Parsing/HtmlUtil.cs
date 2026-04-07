namespace Reddit98Client
{
    public static class HtmlUtil
    {
        public static string DecodeBasic(
            string text)
        {
            if (text == null)
                return null;

            return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&nbsp;", " ")
            .Replace("<br>", "\r\n")
            .Replace("<br/>", "\r\n")
            .Replace("<br />", "\r\n");
        }
    }
}
