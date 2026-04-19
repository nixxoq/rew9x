using System.Windows.Forms;

using ReW9x.Utils;

namespace ReW9x.UI
{
    internal static class MarkdownLinkLabelHelper
    {
        public static MarkdownText Apply(
            LinkLabel label,
            string rawText)
        {
            MarkdownText formatted =
            MarkdownTextFormatter.Format(
                rawText);

            ApplyFormatted(
                label,
                formatted);

            return formatted;
        }

        public static void ApplyFormatted(
            LinkLabel label,
            MarkdownText formatted)
        {
            int i;

            if (formatted == null)
                formatted =
                MarkdownTextFormatter.Format("");

            label.Text =
            formatted.Text;

            label.Links.Clear();

            for (i = 0;
                 i < formatted.Links.Count;
                 i++)
            {
                MarkdownLinkSpan span =
                formatted.Links[i];

                if (span.Start < 0 ||
                    span.Length <= 0 ||
                    span.Start + span.Length > formatted.Text.Length)
                    continue;

                label.Links.Add(
                    span.Start,
                    span.Length,
                    span.Url);
            }
        }
    }
}
