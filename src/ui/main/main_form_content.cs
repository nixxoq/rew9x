using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using ReW9x.Models;

namespace ReW9x.UI
{
    public sealed partial class MainForm
    {
        private void RefreshFeed(
            bool clear)
        {
            if (feedLoading)
                return;

            feedLoading = true;

            if (clear)
            {
                feedPanel.Controls.Clear();
                currentAfter = null;
                lastFeedViewportKey = "";
            }

            SaveCurrentAccountState();

            SetFeedLoadingStatus();

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    RedditListingPage page =
                    api.LoadFeed(
                        currentFeedName,
                        currentAfter,
                        AppConfig.FeedPageSize);

                    currentAfter =
                    page.After;

                    UpdateUi(delegate()
                    {
                        int i;

                        if (clear &&
                            page.Posts.Count == 0)
                        {
                            feedPanel.Controls.Add(
                                MakeLabel(
                                    IsSearchFeed()
                                    ? "No search results."
                                    : "No posts."));
                        }

                        for (i = 0;
                             i < page.Posts.Count;
                             i++)
                        {
                            AddFeedCard(
                                page.Posts[i]);
                        }

                        UpdateFeedCardWidths();
                        feedLoading = false;

                        SetFeedLoadedStatus();

                        CheckFeedScrollPosition(true);
                    });
                }
                catch (Exception ex)
                {
                    UpdateUi(delegate()
                    {
                        if (clear)
                        {
                            feedPanel.Controls.Clear();
                            feedPanel.Controls.Add(
                                MakeLabel(
                                    IsSearchFeed()
                                    ? "Search failed: " +
                                      ex.Message
                                    : ex.Message));
                        }

                        SetStatusText(
                            ex.Message);
                        feedLoading = false;
                    });
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void AddFeedCard(
            RedditPost post)
        {
            PostCardControl card =
            new PostCardControl(post);

            card.Width =
            GetFeedCardWidth();

            card.PostSelected +=
            new EventHandler(
                Card_PostSelected);

            feedPanel.Controls.Add(card);
        }

        private bool IsSearchFeed()
        {
            return
            currentFeedName != null &&
            currentFeedName.StartsWith(
                "search:",
                StringComparison.OrdinalIgnoreCase);
        }

        private void Card_PostSelected(
            object sender,
            EventArgs e)
        {
            PostCardControl card =
            sender as PostCardControl;

            if (card == null)
                return;

            ShowPost(card.PostValue);
        }

        private void ShowPost(
            RedditPost post)
        {
            currentPost = post;
            currentPostPreviewLoaded = false;

            postTitleLabel.Text =
            post.Title;

            postMetaLabel.Text =
            "r/" +
            post.Subreddit +
            " • u/" +
            post.Author;

            if (post.IsSelf)
            {
                if (string.IsNullOrEmpty(post.SelfText))
                    currentPostFallbackText = "";
                else
                    currentPostFallbackText = post.SelfText;
            }
            else
            {
                currentPostFallbackText =
                post.Url;
            }

            postBodyBox.Text =
            currentPostFallbackText;

            UpdatePostLayoutMetrics();
            StartPostMediaLoad(post);

            rightTabs.SelectedTab =
            commentsTab;

            commentsPanel.Controls.Clear();
            commentsPanel.Controls.Add(
                MakeLabel(
                    "Loading comments..."));

            SetCommentsLoadingStatus();

            commentsReady = false;
            currentCommentItems.Clear();
            currentCommentRenderIndex = 0;
            lastCommentViewportKey = "";

            currentCommentRequestId++;
            int requestId =
            currentCommentRequestId;

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    RedditThread thread =
                    api.LoadComments(
                        post.Permalink,
                        "confidence",
                        AppConfig.CommentPageLimit,
                        20);

                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentCommentRequestId)
                            return;

                        commentsPanel.Controls.Clear();

                        currentCommentItems =
                        FlattenComments(
                            thread.Comments);

                        currentCommentRenderIndex =
                        0;

                        commentsReady = true;

                        if (currentCommentItems.Count == 0)
                        {
                            commentsPanel.Controls.Add(
                                MakeLabel(
                                    "No comments."));

                            SetStatusText(
                                "Comments: 0 rendered");
                        }
                        else
                        {
                            RenderNextComments();
                            UpdateCommentControlWidths();
                            CheckCommentsScrollPosition(
                                true);

                            SetCommentsRenderedStatus(
                                "thread loaded");
                        }
                    });
                }
                catch (Exception ex)
                {
                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentCommentRequestId)
                            return;

                        commentsPanel.Controls.Clear();
                        commentsPanel.Controls.Add(
                            MakeLabel(
                                ex.Message));
                        commentsReady = false;

                        SetStatusText(
                            ex.Message);
                    });
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void StartPostMediaLoad(
            RedditPost post)
        {
            currentPostImageRequestId++;
            int requestId =
            currentPostImageRequestId;

            ClearCurrentPostImage();
            currentPostPreviewLoaded = false;

            string imageUrl =
            PostMediaHelper.GetPreferredImageUrl(post);

            if (string.IsNullOrEmpty(imageUrl))
            {
                postMediaPanel.Visible =
                false;
                postMediaMessageLabel.Text =
                "";
                postImageBox.Image =
                null;
                UpdatePostBodyVisibility();
                return;
            }

            postMediaPanel.Visible =
            true;
            postImageBox.Image =
            null;
            postImageBox.Width = 1;
            postImageBox.Height = 1;
            postMediaMessageLabel.Text =
            "Loading image...";

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    byte[] bytes =
                    api.DownloadBytes(imageUrl);

                    Image image =
                    PostMediaHelper.BuildDetachedImage(bytes);

                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentPostImageRequestId)
                        {
                            if (image != null)
                                image.Dispose();
                            return;
                        }

                        currentPostImage =
                        image;

                        currentPostPreviewLoaded =
                        currentPostImage != null;

                        postMediaMessageLabel.Text =
                        "";

                        UpdatePostBodyVisibility();
                        UpdatePostImageLayout();
                    });
                }
                catch (Exception ex)
                {
                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentPostImageRequestId)
                            return;

                        currentPostPreviewLoaded =
                        false;
                        postMediaPanel.Visible =
                        false;

                        currentPostFallbackText =
                        imageUrl + "\r\n\r\n" +
                        ex.Message;

                        UpdatePostBodyVisibility();
                    });
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void ClearCurrentPostImage()
        {
            if (currentPostImage != null)
            {
                postImageBox.Image = null;
                currentPostImage.Dispose();
                currentPostImage = null;
            }
        }

        private void PostImageBox_DoubleClick(
            object sender,
            EventArgs e)
        {
            if (currentPostImage == null)
                return;

            ImageViewerForm viewer =
            new ImageViewerForm(
                new Bitmap(currentPostImage),
                currentPost != null
                ? currentPost.Title
                : "Image Viewer");

            viewer.Show(this);
        }

        private int RenderNextComments()
        {
            int added = 0;

            while (currentCommentRenderIndex <
                   currentCommentItems.Count &&
                   added < CommentBatchSize)
            {
                AddRenderedCommentControl(
                    currentCommentItems[
                        currentCommentRenderIndex]);

                currentCommentRenderIndex++;
                added++;
            }

            return added;
        }

        private void AddRenderedCommentControl(
            CommentRenderItem item)
        {
            Control control =
            CreateCommentControl(item);

            commentsPanel.Controls.Add(control);
        }

        private Control CreateCommentControl(
            CommentRenderItem item)
        {
            int width =
            GetCommentControlWidth();

            if (item.IsMore)
            {
                MoreCommentsControl more =
                new MoreCommentsControl(
                    item.Depth,
                    item.MoreChildrenIds != null
                    ? item.MoreChildrenIds.Length
                    : 0);

                more.Tag = item;
                more.ApplyLayoutWidth(width);
                more.LoadRequested +=
                new EventHandler(
                    MoreControl_LoadRequested);

                return more;
            }

            CommentNodeControl node =
            new CommentNodeControl(
                item.Comment);

            node.Tag = item;
            node.ApplyLayoutWidth(width);

            return node;
        }

        private void MoreControl_LoadRequested(
            object sender,
            EventArgs e)
        {
            MoreCommentsControl control =
            sender as MoreCommentsControl;

            if (control == null)
                return;

            CommentRenderItem item =
            control.Tag as CommentRenderItem;

            if (item == null ||
                item.MoreChildrenIds == null ||
                item.MoreChildrenIds.Length == 0 ||
                currentPost == null)
                return;

            int requestCount =
            item.MoreChildrenIds.Length;

            if (requestCount >
                AppConfig.MoreChildrenLimit)
                requestCount =
                AppConfig.MoreChildrenLimit;

            string[] requestedChildren =
            new string[requestCount];

            Array.Copy(
                item.MoreChildrenIds,
                0,
                requestedChildren,
                0,
                requestCount);

            int requestId =
            currentCommentRequestId;

            RedditPost targetPost =
            currentPost;

            control.SetLoading(true);

            SetStatusText(
                "Loading more comments...");

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    List<RedditComment> moreComments =
                    api.LoadMoreChildren(
                        targetPost.FullName,
                        requestedChildren,
                        "confidence");

                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentCommentRequestId)
                            return;

                        if (control.IsDisposed)
                            return;

                        ApplyMoreCommentsResult(
                            item,
                            control,
                            moreComments,
                            requestCount);

                        SetCommentsRenderedStatus(
                            "more replies loaded");
                    });
                }
                catch (Exception ex)
                {
                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            currentCommentRequestId)
                            return;

                        if (control.IsDisposed)
                            return;

                        control.SetError(ex.Message);
                        SetStatusText(
                            ex.Message);
                    });
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void ApplyMoreCommentsResult(
            CommentRenderItem placeholder,
            MoreCommentsControl control,
            List<RedditComment> moreComments,
            int consumedCount)
        {
            int itemIndex =
            currentCommentItems.IndexOf(
                placeholder);

            if (itemIndex < 0)
                return;

            int panelIndex =
            commentsPanel.Controls.IndexOf(
                control);

            List<CommentRenderItem> insertedItems =
            FlattenComments(moreComments);

            if (placeholder.MoreChildrenIds != null &&
                consumedCount <
                placeholder.MoreChildrenIds.Length)
            {
                int remainingCount =
                placeholder.MoreChildrenIds.Length -
                consumedCount;

                string[] remaining =
                new string[remainingCount];

                Array.Copy(
                    placeholder.MoreChildrenIds,
                    consumedCount,
                    remaining,
                    0,
                    remainingCount);

                CommentRenderItem remainder =
                new CommentRenderItem();

                remainder.IsMore = true;
                remainder.Depth = placeholder.Depth;
                remainder.MoreChildrenIds = remaining;

                insertedItems.Add(remainder);
            }

            commentsPanel.Controls.Remove(control);
            control.Dispose();

            currentCommentItems.RemoveAt(itemIndex);

            if (insertedItems.Count > 0)
                currentCommentItems.InsertRange(
                    itemIndex,
                    insertedItems);

            currentCommentRenderIndex +=
            insertedItems.Count - 1;

            if (currentCommentRenderIndex < 0)
                currentCommentRenderIndex = 0;

            if (panelIndex < 0)
                panelIndex =
                commentsPanel.Controls.Count;

            InsertCommentControls(
                panelIndex,
                insertedItems);

            UpdateCommentControlWidths();
            CheckCommentsScrollPosition(true);
        }

        private void InsertCommentControls(
            int panelIndex,
            List<CommentRenderItem> items)
        {
            int i;

            for (i = 0;
                 i < items.Count;
                 i++)
            {
                Control control =
                CreateCommentControl(items[i]);

                commentsPanel.Controls.Add(control);
                commentsPanel.Controls.SetChildIndex(
                    control,
                    panelIndex + i);
            }
        }

        private List<CommentRenderItem> FlattenComments(
            List<RedditComment> comments)
        {
            List<CommentRenderItem> items =
            new List<CommentRenderItem>();

            if (comments == null)
                return items;

            Stack<RedditComment> stack =
            new Stack<RedditComment>();

            int i;

            for (i = comments.Count - 1;
                 i >= 0;
                 i--)
            {
                if (comments[i] != null)
                    stack.Push(comments[i]);
            }

            while (stack.Count > 0)
            {
                RedditComment comment =
                stack.Pop();

                CommentRenderItem item =
                new CommentRenderItem();

                item.Comment = comment;
                item.IsMore = comment.IsMore;
                item.Depth = comment.Depth;

                if (comment.IsMore)
                    item.MoreChildrenIds =
                    comment.MoreChildrenIds.ToArray();

                items.Add(item);

                if (comment.IsMore)
                    continue;

                for (i = comment.Replies.Count - 1;
                     i >= 0;
                     i--)
                {
                    if (comment.Replies[i] != null)
                        stack.Push(comment.Replies[i]);
                }
            }

            return items;
        }

        private void FeedPanel_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            HideSearchPopup();
            CheckFeedScrollPosition(false);
        }

        private void LoadMoreFeed()
        {
            if (feedLoading)
                return;

            if (string.IsNullOrEmpty(currentAfter))
                return;

            RefreshFeed(false);
        }

        private void CommentsPanel_Scroll(
            object sender,
            ScrollEventArgs e)
        {
            HideSearchPopup();
            CheckCommentsScrollPosition(false);
        }

        private void ScrollPollTimer_Tick(
            object sender,
            EventArgs e)
        {
            CheckFeedScrollPosition(false);
            CheckCommentsScrollPosition(false);
        }

        private void Split_SplitterMoved(
            object sender,
            SplitterEventArgs e)
        {
            Panels_LayoutChanged(
                sender,
                EventArgs.Empty);
        }

        private void Panels_LayoutChanged(
            object sender,
            EventArgs e)
        {
            UpdateTopBarLayout();
            UpdateFeedCardWidths();
            UpdateCommentControlWidths();
            UpdatePostLayoutMetrics();

            lastFeedViewportKey = "";
            lastCommentViewportKey = "";

            CheckFeedScrollPosition(true);
            CheckCommentsScrollPosition(true);
        }

        private void PostTab_Resize(
            object sender,
            EventArgs e)
        {
            UpdatePostLayoutMetrics();
        }

        private void PostMediaScrollPanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdatePostImageLayout();
        }

        private void UpdatePostLayoutMetrics()
        {
            if (!uiBuilt)
                return;

            if (postTitleLabel == null ||
                postMetaLabel == null ||
                postTab == null)
                return;

            int width =
            postTab.ClientSize.Width - 12;

            if (width < 80)
                width = 80;

            postTitleLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                postTitleLabel.Text,
                postTitleLabel.Font,
                width) + 6;

            postMetaLabel.Height =
            TextLayoutHelper.MeasureTextHeight(
                postMetaLabel.Text,
                postMetaLabel.Font,
                width) + 4;

            UpdatePostBodyVisibility();
            UpdatePostImageLayout();
        }

        private void UpdatePostBodyVisibility()
        {
            if (!uiBuilt)
                return;

            if (postBodyBox == null)
                return;

            if (currentPost != null &&
                !currentPost.IsSelf &&
                currentPostPreviewLoaded)
            {
                postBodyBox.Visible =
                false;
                postBodyBox.Text = "";
                return;
            }

            postBodyBox.Visible = true;
            postBodyBox.Text =
            currentPostFallbackText;
        }

        private void UpdatePostImageLayout()
        {
            if (!uiBuilt)
                return;

            if (postMediaPanel == null ||
                postMediaScrollPanel == null ||
                postImageBox == null)
                return;

            if (currentPostImage == null)
            {
                postImageBox.Image = null;
                postMediaMessageLabel.Visible =
                !string.IsNullOrEmpty(
                    postMediaMessageLabel.Text);
                return;
            }

            int availableWidth =
            postMediaScrollPanel.ClientSize.Width - 20;

            if (availableWidth < 80)
                availableWidth = 80;

            int displayWidth =
            currentPostImage.Width;

            int displayHeight =
            currentPostImage.Height;

            if (displayWidth > availableWidth)
            {
                displayHeight =
                (currentPostImage.Height *
                 availableWidth) /
                currentPostImage.Width;

                displayWidth =
                availableWidth;
            }

            if (displayHeight < 40)
                displayHeight = 40;

            postImageBox.Image =
            currentPostImage;

            postImageBox.Left = 0;
            postImageBox.Top = 0;
            postImageBox.Width =
            displayWidth;
            postImageBox.Height =
            displayHeight;

            postMediaMessageLabel.Text =
            "";
            postMediaMessageLabel.Visible =
            false;

            postMediaPanel.Height =
            GetPreferredPostMediaHeight(
                displayHeight + 8);
        }

        private int GetPreferredPostMediaHeight(
            int requestedHeight)
        {
            int available =
            postTab.ClientSize.Height -
            postTitleLabel.Height -
            postMetaLabel.Height -
            12;

            if (postBodyBox.Visible)
                available -= 120;

            if (available < 120)
                available = 120;

            if (requestedHeight > available)
                return available;

            return requestedHeight;
        }

        private void CheckFeedScrollPosition(
            bool force)
        {
            if (!uiBuilt)
                return;

            string key =
            BuildViewportKey(feedPanel);

            if (!force && key == lastFeedViewportKey)
                return;

            lastFeedViewportKey = key;

            if (IsNearBottom(feedPanel))
                LoadMoreFeed();
        }

        private void CheckCommentsScrollPosition(
            bool force)
        {
            if (!uiBuilt)
                return;

            if (!commentsReady)
                return;

            string key =
            BuildViewportKey(commentsPanel);

            if (!force && key == lastCommentViewportKey)
                return;

            lastCommentViewportKey = key;

            if (IsNearBottom(commentsPanel))
            {
                if (RenderNextComments() > 0)
                    SetCommentsRenderedStatus(null);
            }
        }

        private string BuildViewportKey(
            ScrollableControl control)
        {
            if (control == null)
                return "";

            if (control.ClientSize.Height <= 0)
                return "";

            return
            control.VerticalScroll.Value.ToString() +
            ":" +
            control.ClientSize.Height.ToString() +
            ":" +
            control.DisplayRectangle.Height.ToString();
        }

        private bool IsNearBottom(
            ScrollableControl control)
        {
            if (control == null)
                return false;

            if (control.ClientSize.Height <= 0)
                return false;

            int bottom =
            control.VerticalScroll.Value +
            control.ClientSize.Height;

            int max =
            control.DisplayRectangle.Height;

            return bottom >= max - 120;
        }

        private void UpdateFeedCardWidths()
        {
            if (!uiBuilt)
                return;

            if (feedPanel == null)
                return;

            int width =
            GetFeedCardWidth();

            int i;

            for (i = 0;
                 i < feedPanel.Controls.Count;
                 i++)
            {
                PostCardControl card =
                feedPanel.Controls[i]
                as PostCardControl;

                if (card != null)
                    card.Width = width;
            }
        }

        private void UpdateCommentControlWidths()
        {
            if (!uiBuilt)
                return;

            if (commentsPanel == null)
                return;

            int width =
            GetCommentControlWidth();

            int i;

            for (i = 0;
                 i < commentsPanel.Controls.Count;
                 i++)
            {
                CommentNodeControl node =
                commentsPanel.Controls[i]
                as CommentNodeControl;

                if (node != null)
                {
                    node.ApplyLayoutWidth(width);
                    continue;
                }

                MoreCommentsControl more =
                commentsPanel.Controls[i]
                as MoreCommentsControl;

                if (more != null)
                    more.ApplyLayoutWidth(width);
            }
        }

        private int GetFeedCardWidth()
        {
            if (feedPanel == null)
                return 180;

            if (feedPanel.ClientSize.Width <= 0)
                return 180;

            int width =
            feedPanel.ClientSize.Width -
            SystemInformation.VerticalScrollBarWidth -
            12;

            if (width < 180)
                width = 180;

            return width;
        }

        private int GetCommentControlWidth()
        {
            if (commentsPanel == null)
                return 160;

            if (commentsPanel.ClientSize.Width <= 0)
                return 160;

            int width =
            commentsPanel.ClientSize.Width -
            SystemInformation.VerticalScrollBarWidth -
            12;

            if (width < 160)
                width = 160;

            return width;
        }
    }
}
