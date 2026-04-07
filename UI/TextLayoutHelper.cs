using System.Drawing;
using System.Windows.Forms;

namespace Reddit98Client
{
    public static class TextLayoutHelper
    {
        public static int MeasureTextHeight(
            string text,
            Font font,
            int width)
        {
            return MeasureTextHeight(
                text,
                font,
                width,
                true);
        }

        public static int MeasureTextHeight(
            string text,
            Font font,
            int width,
            bool wordBreak)
        {
            if (text == null)
                text = "";

            TextFormatFlags flags =
            TextFormatFlags.Left;

            if (wordBreak)
                flags |= TextFormatFlags.WordBreak;
            else
                flags |= TextFormatFlags.SingleLine;

            Size measured =
            TextRenderer.MeasureText(
                text + " ",
                font,
                new Size(width, 32767),
                flags);

            if (measured.Height < font.Height + 2)
                return font.Height + 2;

            return measured.Height;
        }
    }
}
