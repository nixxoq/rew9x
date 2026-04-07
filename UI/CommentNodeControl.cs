using System;
using System.Drawing;
using System.Windows.Forms;

namespace Reddit98Client
{
    public sealed class CommentNodeControl
    : Panel
    {
        private const int IndentSize = 12;
        private const int MaxVisibleIndentLevel = 5;
        private const int GutterStart = 8;
        private const int InnerPadding = 4;

        private Label authorLabel;
        private Label bodyLabel;
        private int displayDepth;

        public CommentNodeControl(
            RedditComment comment)
        {
            BorderStyle =
            BorderStyle.None;

            Margin =
            new Padding(2, 1, 2, 1);

            BackColor =
            Color.White;

            displayDepth =
            GetDisplayDepth(
                comment.Depth);

            BuildUi(comment);
        }

        private void BuildUi(
            RedditComment c)
        {
            authorLabel =
            new Label();

            authorLabel.Text =
            "u/" +
            SafeAuthor(c.Author) +
            " • " +
            c.Score +
            " points";

            authorLabel.Font =
            new Font(
                Font,
                FontStyle.Bold);

            authorLabel.AutoSize =
            false;

            bodyLabel =
            new Label();

            bodyLabel.Text =
            HtmlUtil.DecodeBasic(c.Body);

            bodyLabel.AutoSize =
            false;

            Controls.Add(bodyLabel);
            Controls.Add(authorLabel);

            ApplyLayoutWidth(240);
        }

        protected override void OnResize(
            EventArgs e)
        {
            base.OnResize(e);

            ApplyLayoutWidth(
                Width);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            DrawThreadGuides(
                e.Graphics,
                Height);
        }

        public void ApplyLayoutWidth(
            int totalWidth)
        {
            int safeWidth =
            totalWidth;

            if (safeWidth < 120)
                safeWidth = 120;

            if (Width != safeWidth)
                Width = safeWidth;

            int contentLeft =
            GetContentLeft();

            int contentWidth =
            ClientSize.Width -
            contentLeft -
            InnerPadding;

            if (contentWidth < 40)
                contentWidth = 40;

            int authorHeight =
            TextLayoutHelper.MeasureTextHeight(
                authorLabel.Text,
                authorLabel.Font,
                contentWidth);

            int bodyHeight =
            TextLayoutHelper.MeasureTextHeight(
                bodyLabel.Text,
                bodyLabel.Font,
                contentWidth);

            authorLabel.Left =
            contentLeft;

            authorLabel.Top =
            InnerPadding;

            authorLabel.Width =
            contentWidth;

            authorLabel.Height =
            authorHeight;

            bodyLabel.Left =
            contentLeft;

            bodyLabel.Top =
            authorLabel.Bottom + 2;

            bodyLabel.Width =
            contentWidth;

            bodyLabel.Height =
            bodyHeight;

            Height =
            bodyLabel.Bottom +
            InnerPadding + 2;

            Invalidate();
        }

        private void DrawThreadGuides(
            Graphics g,
            int height)
        {
            if (height <= 0)
                return;

            Pen guidePen =
            Pens.Silver;

            int i;
            for (i = 0;
                 i <= displayDepth;
                 i++)
            {
                int x =
                GutterStart +
                (i * IndentSize);

                g.DrawLine(
                    guidePen,
                    x,
                    0,
                    x,
                    height);
            }

            int currentX =
            GutterStart +
            (displayDepth * IndentSize);

            int elbowY =
            authorLabel.Top +
            (authorLabel.Height / 2);

            g.DrawLine(
                Pens.Gray,
                currentX,
                elbowY,
                GetContentLeft() - 4,
                elbowY);

            g.FillEllipse(
                Brushes.White,
                currentX - 4,
                elbowY - 4,
                8,
                8);

            g.DrawEllipse(
                Pens.Gray,
                currentX - 4,
                elbowY - 4,
                8,
                8);
        }

        private int GetContentLeft()
        {
            return
            GutterStart +
            ((displayDepth + 1) * IndentSize) +
            6;
        }

        private static int GetDisplayDepth(
            int depth)
        {
            if (depth < 0)
                return 0;

            if (depth > MaxVisibleIndentLevel)
                return MaxVisibleIndentLevel;

            return depth;
        }

        private static string SafeAuthor(
            string author)
        {
            if (string.IsNullOrEmpty(author))
                return "[deleted]";

            return author;
        }

    }
}
