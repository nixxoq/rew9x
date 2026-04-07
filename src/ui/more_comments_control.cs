using System;
using System.Drawing;
using System.Windows.Forms;

using ReW9x;
using ReW9x.Api;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.UI
{
    public sealed class MoreCommentsControl
    : Panel
    {
        private const int IndentSize = 12;
        private const int MaxVisibleIndentLevel = 5;
        private const int GutterStart = 8;
        private const int InnerPadding = 4;

        private Button loadButton;
        private Label statusLabel;
        private int displayDepth;

        public event EventHandler LoadRequested;

        public MoreCommentsControl(
            int depth,
            int childCount)
        {
            BorderStyle =
            BorderStyle.None;

            Margin =
            new Padding(2, 1, 2, 1);

            BackColor =
            Color.White;

            displayDepth =
            GetDisplayDepth(depth);

            BuildUi(childCount);
        }

        private void BuildUi(
            int childCount)
        {
            loadButton =
            new Button();

            loadButton.Text =
            BuildButtonText(childCount);

            loadButton.Click +=
            new EventHandler(
                LoadButton_Click);

            statusLabel =
            new Label();

            statusLabel.AutoSize =
            false;

            statusLabel.ForeColor =
            Color.DimGray;

            Controls.Add(statusLabel);
            Controls.Add(loadButton);

            ApplyLayoutWidth(220);
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

            if (safeWidth < 140)
                safeWidth = 140;

            if (Width != safeWidth)
                Width = safeWidth;

            int contentLeft =
            GetContentLeft();

            int contentWidth =
            ClientSize.Width -
            contentLeft -
            InnerPadding;

            if (contentWidth < 80)
                contentWidth = 80;

            loadButton.Left =
            contentLeft;

            loadButton.Top =
            InnerPadding;

            loadButton.Width =
            contentWidth;

            loadButton.Height =
            24;

            statusLabel.Left =
            contentLeft;

            statusLabel.Top =
            loadButton.Bottom + 2;

            statusLabel.Width =
            contentWidth;

            statusLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                statusLabel.Text,
                statusLabel.Font,
                contentWidth);

            Height =
            statusLabel.Bottom +
            InnerPadding + 2;

            Invalidate();
        }

        public void SetLoading(
            bool loading)
        {
            loadButton.Enabled =
            !loading;

            if (loading)
                statusLabel.Text =
                "Loading...";
            else
                statusLabel.Text =
                "";

            ApplyLayoutWidth(
                Width);
        }

        public void SetError(
            string message)
        {
            loadButton.Enabled =
            true;

            statusLabel.Text =
            message;

            ApplyLayoutWidth(
                Width);
        }

        private void DrawThreadGuides(
            Graphics g,
            int height)
        {
            if (height <= 0)
                return;

            int i;
            for (i = 0;
                 i <= displayDepth;
                 i++)
            {
                int x =
                GutterStart +
                (i * IndentSize);

                g.DrawLine(
                    Pens.Silver,
                    x,
                    0,
                    x,
                    height);
            }

            int currentX =
            GutterStart +
            (displayDepth * IndentSize);

            int elbowY =
            loadButton.Top +
            (loadButton.Height / 2);

            g.DrawLine(
                Pens.Gray,
                currentX,
                elbowY,
                GetContentLeft() - 4,
                elbowY);
        }

        private void LoadButton_Click(
            object sender,
            EventArgs e)
        {
            if (LoadRequested != null)
                LoadRequested(
                    this,
                    EventArgs.Empty);
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

        private static string BuildButtonText(
            int childCount)
        {
            if (childCount > 0)
                return
                "Load more comments (" +
                childCount.ToString() +
                ")";

            return
            "Load more comments";
        }
    }
}
