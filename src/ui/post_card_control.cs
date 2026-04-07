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
    public sealed class PostCardControl : UserControl
    {
        private const int HorizontalPadding = 6;
        private const int VerticalPadding = 6;
        private const int MinimumCardHeight = 86;

        private Label titleLabel;
        private Label metaLabel;
        private Label scoreLabel;
        private Panel innerPanel;
        private RedditPost _post;

        public event EventHandler PostSelected;

        public PostCardControl(RedditPost post)
        {
            _post = post;
            Height = MinimumCardHeight;
            Width = 100;
            Margin = new Padding(4);
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Color.White;

            BuildUi();
            Bind(post);
        }

        public RedditPost PostValue
        {
            get { return _post; }
        }

        private void BuildUi()
        {
            innerPanel = new Panel();
            innerPanel.Dock = DockStyle.Fill;

            scoreLabel = new Label();
            scoreLabel.AutoSize = false;
            scoreLabel.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);

            titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Font = new Font("Tahoma", 8.5f, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.TopLeft;

            metaLabel = new Label();
            metaLabel.AutoSize = false;
            metaLabel.ForeColor = Color.DimGray;

            innerPanel.Controls.Add(scoreLabel);
            innerPanel.Controls.Add(titleLabel);
            innerPanel.Controls.Add(metaLabel);
            Controls.Add(innerPanel);

            HookClicks(this);
        }

        protected override void OnResize(
            EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutMetrics();
        }

        private void HookClicks(
            Control c)
        {
            c.Click +=
            new EventHandler(
                CardClicked);

            int i;
            for (i = 0;
                 i < c.Controls.Count;
                 i++)
            {
                HookClicks(c.Controls[i]);
            }
        }

        private void CardClicked(
            object sender,
            EventArgs e)
        {
            if (PostSelected != null)
                PostSelected(
                    this,
                    EventArgs.Empty);
        }

        private void Bind(
            RedditPost post)
        {
            if (post == null)
                return;

            scoreLabel.Text =
            post.Score.ToString() +
            " points";

            titleLabel.Text =
            post.Title;

            metaLabel.Text =
            "r/" +
            post.Subreddit +
            " • u/" +
            post.Author +
            " • " +
            post.NumComments.ToString() +
            " comments";

            UpdateLayoutMetrics();
        }

        private void UpdateLayoutMetrics()
        {
            if (innerPanel == null)
                return;

            int contentWidth =
            ClientSize.Width -
            (HorizontalPadding * 2);

            if (contentWidth < 80)
                contentWidth = 80;

            scoreLabel.Left =
            HorizontalPadding;

            scoreLabel.Top =
            VerticalPadding;

            scoreLabel.Width =
            contentWidth;

            scoreLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                scoreLabel.Text,
                scoreLabel.Font,
                contentWidth,
                false);

            titleLabel.Left =
            HorizontalPadding;

            titleLabel.Top =
            scoreLabel.Bottom + 4;

            titleLabel.Width =
            contentWidth;

            titleLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                titleLabel.Text,
                titleLabel.Font,
                contentWidth,
                true);

            metaLabel.Left =
            HorizontalPadding;

            metaLabel.Top =
            titleLabel.Bottom + 4;

            metaLabel.Width =
            contentWidth;

            metaLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                metaLabel.Text,
                metaLabel.Font,
                contentWidth,
                false);

            Height =
            Math.Max(
                MinimumCardHeight,
                metaLabel.Bottom +
                VerticalPadding);
        }

    }
}
