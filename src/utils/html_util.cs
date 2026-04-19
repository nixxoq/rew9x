using System;
using System.Text;

namespace ReW9x.Utils
{
    public static class HtmlUtil
    {
        public static string DecodeBasic(
            string text)
        {
            if (text == null)
                return null;

            return NormalizeLineBreaks(
                DecodeEntities(
                    DecodeBreakTags(text)));
        }

        private static string DecodeBreakTags(
            string text)
        {
            return text
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("<BR>", "\n")
            .Replace("<BR/>", "\n")
            .Replace("<BR />", "\n");
        }

        private static string DecodeEntities(
            string text)
        {
            StringBuilder sb =
            new StringBuilder(
                text.Length);

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '&')
                {
                    int semi =
                    text.IndexOf(
                        ';',
                        i + 1);

                    if (semi > i &&
                        semi - i <= 12)
                    {
                        string entity =
                        text.Substring(
                            i + 1,
                            semi - i - 1);

                        string decoded =
                        DecodeEntity(entity);

                        if (decoded != null)
                        {
                            sb.Append(decoded);
                            i = semi + 1;
                            continue;
                        }
                    }
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        private static string DecodeEntity(
            string entity)
        {
            if (entity == "amp")
                return "&";
            if (entity == "lt")
                return "<";
            if (entity == "gt")
                return ">";
            if (entity == "quot")
                return "\"";
            if (entity == "apos")
                return "'";
            if (entity == "nbsp")
                return " ";
            if (entity == "ndash")
                return "-";
            if (entity == "mdash")
                return "-";
            if (entity == "hellip")
                return "...";
            if (entity == "rsquo" ||
                entity == "lsquo")
                return "'";
            if (entity == "rdquo" ||
                entity == "ldquo")
                return "\"";

            if (entity.Length > 1 &&
                entity[0] == '#')
                return DecodeNumericEntity(entity);

            return null;
        }

        private static string DecodeNumericEntity(
            string entity)
        {
            int value;

            try
            {
                if (entity.Length > 2 &&
                    (entity[1] == 'x' ||
                     entity[1] == 'X'))
                {
                    value =
                    Convert.ToInt32(
                        entity.Substring(2),
                        16);
                }
                else
                {
                    value =
                    Convert.ToInt32(
                        entity.Substring(1),
                        10);
                }
            }
            catch
            {
                return null;
            }

            if (value <= 0)
                return null;

            if (value == 0x2018 ||
                value == 0x2019)
                return "'";
            if (value == 0x201C ||
                value == 0x201D)
                return "\"";
            if (value == 0x2013 ||
                value == 0x2014)
                return "-";
            if (value == 0x2026)
                return "...";

            if (value > 0xFFFF)
                return null;

            return new string(
                (char)value,
                1);
        }

        public static string NormalizeLineBreaks(
            string text)
        {
            if (text == null)
                return null;

            text =
            text.Replace(
                "\r\n",
                "\n");

            text =
            text.Replace(
                "\r",
                "\n");

            return text.Replace(
                "\n",
                "\r\n");
        }
    }
}
