using System.Collections.Generic;
using System.Text;

namespace ReW9x.Utils
{
    public sealed class MarkdownLinkSpan
    {
        public int Start;
        public int Length;
        public string Url;
    }

    public sealed class MarkdownText
    {
        public string Text;
        public List<MarkdownLinkSpan> Links =
        new List<MarkdownLinkSpan>();
    }

    public static class MarkdownTextFormatter
    {
        public static MarkdownText Format(
            string rawText)
        {
            MarkdownText formatted =
            new MarkdownText();

            string text =
            HtmlUtil.DecodeBasic(
                rawText);

            if (text == null)
                text = "";

            ParseInlineLinks(
                text,
                formatted);

            return formatted;
        }

        private static void ParseInlineLinks(
            string text,
            MarkdownText formatted)
        {
            StringBuilder display =
            new StringBuilder(
                text.Length);

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '[')
                {
                    int closeLabel =
                    text.IndexOf(
                        ']',
                        i + 1);

                    if (closeLabel > i + 1 &&
                        closeLabel + 1 < text.Length &&
                        text[closeLabel + 1] == '(')
                    {
                        int closeUrl =
                        text.IndexOf(
                            ')',
                            closeLabel + 2);

                        if (closeUrl > closeLabel + 2)
                        {
                            string label =
                            text.Substring(
                                i + 1,
                                closeLabel - i - 1);

                            string url =
                            text.Substring(
                                closeLabel + 2,
                                closeUrl - closeLabel - 2);

                            string normalizedUrl =
                            NormalizeUrl(url);

                            if (label.Length > 0 &&
                                normalizedUrl != null)
                            {
                                MarkdownLinkSpan span =
                                new MarkdownLinkSpan();

                                span.Start =
                                display.Length;
                                span.Length =
                                label.Length;
                                span.Url =
                                normalizedUrl;

                                display.Append(label);
                                formatted.Links.Add(span);

                                i = closeUrl + 1;
                                continue;
                            }

                            display.Append(
                                text.Substring(
                                    i,
                                    closeUrl - i + 1));

                            i = closeUrl + 1;
                            continue;
                        }
                    }
                }

                display.Append(text[i]);
                i++;
            }

            formatted.Text =
            display.ToString();
        }

        private static string NormalizeUrl(
            string url)
        {
            if (url == null)
                return null;

            url =
            url.Trim();

            if (url.Length == 0 ||
                HasUnsafeUrlChars(url))
                return null;

            if (StartsWithIgnoreCase(url, "https://") ||
                StartsWithIgnoreCase(url, "http://"))
                return url;

            if (StartsWithIgnoreCase(url, "/r/") ||
                StartsWithIgnoreCase(url, "/u/") ||
                StartsWithIgnoreCase(url, "/comments/"))
                return "https://www.reddit.com" + url;

            return null;
        }

        private static bool HasUnsafeUrlChars(
            string url)
        {
            int i;

            for (i = 0;
                 i < url.Length;
                 i++)
            {
                char c =
                url[i];

                if (c <= 32 ||
                    c == '"' ||
                    c == '\'' ||
                    c == '<' ||
                    c == '>')
                    return true;
            }

            return false;
        }

        private static bool StartsWithIgnoreCase(
            string text,
            string prefix)
        {
            if (text.Length < prefix.Length)
                return false;

            return string.Compare(
                text,
                0,
                prefix,
                0,
                prefix.Length,
                true) == 0;
        }
    }
}
