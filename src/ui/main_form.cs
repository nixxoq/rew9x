using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

using ReW9x;
using ReW9x.Api;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;

namespace ReW9x.UI
{
    public sealed partial class MainForm : Form
    {
        private sealed class FeedCategoryInfo
        {
            public string Label;
            public string FeedName;
            public Button Button;
        }

        private sealed class SearchSuggestionItem
        {
            public string QueryText;
            public string FeedName;
            public string DisplayText;
            public string Section;
            public bool IsSubreddit;

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class CommentRenderItem
        {
            public RedditComment Comment;
            public bool IsMore;
            public int Depth;
            public string[] MoreChildrenIds;
        }

        private SplitContainer split;
        private Panel topBarPanel;
        private Panel feedHostPanel;
        private Panel drawerHandlePanel;
        private Panel drawerPanel;
        private Panel drawerHeaderPanel;
        private Label drawerHeaderLabel;
        private Panel drawerListPanel;
        private Button drawerOpenButton;
        private Button drawerCloseButton;
        private Panel topNavButtonsPanel;
        private Panel searchHostPanel;
        private TextBox searchTextBox;
        private Button searchGoButton;
        private Panel searchPopupPanel;
        private Panel searchPopupResultsPanel;
        private Panel newPostPanel;
        private Button newPostButton;
        private FlowLayoutPanel feedPanel;
        private FlowLayoutPanel commentsPanel;

        private Label postTitleLabel;
        private Label postMetaLabel;
        private TextBox postBodyBox;
        private Panel postMediaPanel;
        private Panel postMediaScrollPanel;
        private PictureBox postImageBox;
        private Label postMediaMessageLabel;

        private TabControl rightTabs;
        private TabPage postTab;
        private TabPage commentsTab;

        private StatusFooterControl statusFooter;
        private System.Windows.Forms.Timer scrollPollTimer;

        private MainMenu mainMenu;
        private MenuItem accountMenu;
        private MenuItem useSavedAccountMenuItem;
        private MenuItem loginMenuItem;
        private MenuItem anonymousMenuItem;

        private readonly AccountStore accountStore;
        private readonly OAuthClient oauthClient;
        private readonly RedditApiClient api;

        private AccountState savedAccountState;
        private string currentFeedName = AppConfig.DefaultFeed;
        private string currentAfter = null;
        private bool feedLoading = false;

        private RedditPost currentPost;
        private Image currentPostImage;
        private int currentPostImageRequestId = 0;
        private bool currentPostPreviewLoaded = false;
        private string currentPostFallbackText = "";

        private string currentUsername;

        private List<CommentRenderItem> currentCommentItems =
        new List<CommentRenderItem>();

        private int currentCommentRenderIndex = 0;
        private int currentCommentRequestId = 0;

        private const int CommentBatchSize = 50;

        private bool commentsReady = false;
        private bool uiBuilt = false;
        private bool updatingSearchItems = false;
        private bool subscriptionsLoading = false;
        private bool searchLoading = false;
        private bool subredditSearchLoading = false;
        private string lastSearchQueryRequested = "";
        private string lastSubredditSearchQueryRequested = "";
        private string currentSearchError = null;
        private bool searchHasFocus = false;

        private string lastFeedViewportKey = "";
        private string lastCommentViewportKey = "";
        private List<FeedCategoryInfo> feedCategories =
        new List<FeedCategoryInfo>();
        private List<RedditSubreddit> subscribedSubreddits =
        new List<RedditSubreddit>();
        private List<RedditSubreddit> subredditSearchResults =
        new List<RedditSubreddit>();
        private List<RedditPost> searchPosts =
        new List<RedditPost>();
        private List<SearchSuggestionItem> currentSearchSuggestions =
        new List<SearchSuggestionItem>();
        private int searchRequestId = 0;
        private int subredditSearchRequestId = 0;

        private static readonly string[] FallbackSubreddits =
        new string[]
        {
            "pics",
            "funny",
            "AskReddit",
            "todayilearned",
            "worldnews",
            "technology",
            "gaming",
            "movies"
        };

        public MainForm()
        {
            Text = "ReW9x (Reddit98Client)";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 750);

            accountStore =
            new AccountStore(
                Path.Combine(
                    Application.StartupPath,
                    AppConfig.AccountFileName));

            oauthClient =
            new OAuthClient(
                AppConfig.ClientId,
                AppConfig.RedirectUri,
                AppConfig.Scope,
                AppConfig.UserAgent);

            savedAccountState =
            accountStore.Load();

            RedditToken token =
            savedAccountState != null
            ? savedAccountState.ToToken()
            : null;

            api =
            new RedditApiClient(
                oauthClient,
                token);

            if (savedAccountState != null)
            {
                if (!string.IsNullOrEmpty(
                    savedAccountState.SelectedFeed))
                {
                    currentFeedName =
                    NormalizeFeedName(
                        savedAccountState.SelectedFeed);
                }

                currentUsername =
                savedAccountState.RedditUsername;
            }

            api.TokenChanged +=
            new EventHandler(
                Api_TokenChanged);

            BuildUi();

            OpenSslWrp.HttpsClient.Initialize(
                Path.Combine(
                    Application.StartupPath,
                    "cacert.pem"),
                false);

            Shown +=
            new EventHandler(
                MainForm_Shown);
        }

        protected override void OnFormClosed(
            FormClosedEventArgs e)
        {
            try
            {
                if (scrollPollTimer != null)
                    scrollPollTimer.Stop();
            }
            catch
            {
            }

            ClearCurrentPostImage();

            try
            {
                OpenSslWrp.HttpsClient.Shutdown();
            }
            catch
            {
            }

            base.OnFormClosed(e);
        }

        private void BuildUi()
        {
            BuildMenu();

            topBarPanel =
            new Panel();

            topBarPanel.Dock =
            DockStyle.Top;

            topBarPanel.Height =
            56;

            topBarPanel.Resize +=
            new EventHandler(
                TopBarPanel_Resize);

            topNavButtonsPanel =
            new Panel();

            topNavButtonsPanel.Left =
            12;

            topNavButtonsPanel.Top =
            12;

            topNavButtonsPanel.Width =
            352;

            topNavButtonsPanel.Height =
            30;

            BuildTopNavigation();

            searchHostPanel =
            new Panel();

            searchHostPanel.BorderStyle =
            BorderStyle.FixedSingle;

            searchHostPanel.Height =
            30;

            searchHostPanel.Top =
            12;

            searchTextBox =
            new TextBox();

            searchTextBox.Left =
            4;

            searchTextBox.Top =
            3;

            searchTextBox.Width =
            260;

            searchTextBox.TextChanged +=
            new EventHandler(
                SearchTextBox_TextChanged);

            searchTextBox.Enter +=
            new EventHandler(
                SearchTextBox_Enter);

            searchTextBox.Leave +=
            new EventHandler(
                SearchTextBox_Leave);

            searchTextBox.KeyDown +=
            new KeyEventHandler(
                SearchTextBox_KeyDown);

            searchGoButton =
            new Button();

            searchGoButton.Text =
            "Go";

            searchGoButton.Width =
            48;

            searchGoButton.Height =
            22;

            searchGoButton.Top =
            3;

            searchGoButton.Click +=
            new EventHandler(
                SearchGoButton_Click);

            searchHostPanel.Controls.Add(
                searchTextBox);

            searchHostPanel.Controls.Add(
                searchGoButton);

            searchPopupPanel =
            new Panel();

            searchPopupPanel.Visible =
            false;

            searchPopupPanel.BorderStyle =
            BorderStyle.FixedSingle;

            searchPopupPanel.Height =
            240;

            searchPopupPanel.VisibleChanged +=
            new EventHandler(
                SearchPopupPanel_VisibleChanged);

            searchPopupResultsPanel =
            new Panel();

            searchPopupResultsPanel.Dock =
            DockStyle.Fill;

            searchPopupResultsPanel.AutoScroll =
            true;

            searchPopupPanel.Controls.Add(
                searchPopupResultsPanel);

            topBarPanel.Controls.Add(
                topNavButtonsPanel);

            topBarPanel.Controls.Add(
                searchHostPanel);

            statusFooter =
            new StatusFooterControl();

            statusFooter.Dock =
            DockStyle.Bottom;

            newPostPanel =
            new Panel();

            newPostPanel.Dock =
            DockStyle.Bottom;

            newPostPanel.Height =
            40;

            newPostPanel.Visible =
            false;

            newPostButton =
            new Button();

            newPostButton.Text =
            "New Post";

            newPostButton.Width =
            120;

            newPostButton.Height =
            26;

            newPostButton.Top =
            7;

            newPostButton.Click +=
            new EventHandler(
                NewPostButton_Click);

            newPostPanel.Resize +=
            new EventHandler(
                NewPostPanel_Resize);

            newPostPanel.Controls.Add(
                newPostButton);

            split =
            new SplitContainer();

            split.Dock =
            DockStyle.Fill;

            split.SplitterDistance =
            520;

            split.SplitterMoved +=
            new SplitterEventHandler(
                Split_SplitterMoved);

            feedHostPanel =
            new Panel();

            feedHostPanel.Dock =
            DockStyle.Fill;

            feedHostPanel.Click +=
            new EventHandler(
                NonSearchSurface_Click);

            drawerHandlePanel =
            new Panel();

            drawerHandlePanel.Dock =
            DockStyle.Left;

            drawerHandlePanel.Width =
            18;

            drawerHandlePanel.BorderStyle =
            BorderStyle.FixedSingle;

            drawerPanel =
            new Panel();
            drawerPanel.Dock =
            DockStyle.Left;
            drawerPanel.Width =
            220;
            drawerPanel.Visible =
            false;
            drawerPanel.BorderStyle =
            BorderStyle.FixedSingle;
            BuildDrawerPanel();

            drawerOpenButton =
            new Button();
            drawerOpenButton.Text =
            ">";
            drawerOpenButton.Width =
            16;
            drawerOpenButton.Height =
            60;
            drawerOpenButton.Left = 0;
            drawerOpenButton.Top = 10;
            drawerOpenButton.Click +=
            new EventHandler(
                DrawerOpenButton_Click);

            drawerHandlePanel.Resize +=
            new EventHandler(
                DrawerHandlePanel_Resize);

            drawerHandlePanel.Controls.Add(
                drawerOpenButton);

            feedPanel =
            new FlowLayoutPanel();

            feedPanel.Dock =
            DockStyle.Fill;

            feedPanel.AutoScroll =
            true;

            feedPanel.FlowDirection =
            FlowDirection.TopDown;

            feedPanel.WrapContents =
            false;

            feedPanel.Resize +=
            new EventHandler(
                Panels_LayoutChanged);

            feedPanel.Scroll +=
            new ScrollEventHandler(
                FeedPanel_Scroll);

            feedHostPanel.Controls.Add(
                feedPanel);

            feedHostPanel.Controls.Add(
                drawerPanel);

            feedHostPanel.Controls.Add(
                drawerHandlePanel);

            split.Panel1.Controls.Add(
                feedHostPanel);

            rightTabs =
            new TabControl();

            rightTabs.Dock =
            DockStyle.Fill;

            rightTabs.Click +=
            new EventHandler(
                NonSearchSurface_Click);

            postTab =
            new TabPage("Post");

            commentsTab =
            new TabPage("Comments");

            postTitleLabel =
            new Label();

            postTitleLabel.Dock =
            DockStyle.Top;

            postTitleLabel.Padding =
            new Padding(6, 6, 6, 0);

            postMetaLabel =
            new Label();

            postMetaLabel.Dock =
            DockStyle.Top;

            postMetaLabel.Padding =
            new Padding(6, 2, 6, 2);

            postMediaPanel =
            new Panel();

            postMediaPanel.Dock =
            DockStyle.Top;

            postMediaPanel.Height =
            220;

            postMediaPanel.Visible =
            false;

            postMediaScrollPanel =
            new Panel();

            postMediaScrollPanel.Dock =
            DockStyle.Fill;

            postMediaScrollPanel.AutoScroll =
            true;

            postMediaScrollPanel.BorderStyle =
            BorderStyle.FixedSingle;

            postMediaScrollPanel.BackColor =
            Color.White;

            postMediaScrollPanel.Resize +=
            new EventHandler(
                PostMediaScrollPanel_Resize);

            postImageBox =
            new PictureBox();

            postImageBox.Left = 0;
            postImageBox.Top = 0;
            postImageBox.SizeMode =
            PictureBoxSizeMode.StretchImage;
            postImageBox.DoubleClick +=
            new EventHandler(
                PostImageBox_DoubleClick);

            postMediaMessageLabel =
            new Label();

            postMediaMessageLabel.AutoSize =
            false;

            postMediaMessageLabel.Left =
            8;

            postMediaMessageLabel.Top =
            8;

            postMediaMessageLabel.Width =
            280;

            postMediaMessageLabel.Height =
            40;

            postMediaMessageLabel.Text =
            "";

            postMediaScrollPanel.Controls.Add(
                postImageBox);

            postMediaScrollPanel.Controls.Add(
                postMediaMessageLabel);

            postMediaPanel.Controls.Add(
                postMediaScrollPanel);

            postBodyBox =
            new TextBox();

            postBodyBox.Dock =
            DockStyle.Fill;

            postBodyBox.Multiline =
            true;

            postBodyBox.ScrollBars =
            ScrollBars.Vertical;

            postBodyBox.ReadOnly =
            true;

            postTab.Resize +=
            new EventHandler(
                PostTab_Resize);

            postTab.Controls.Add(
                postBodyBox);

            postTab.Controls.Add(
                postMediaPanel);

            postTab.Controls.Add(
                postMetaLabel);

            postTab.Controls.Add(
                postTitleLabel);

            commentsPanel =
            new FlowLayoutPanel();

            commentsPanel.Dock =
            DockStyle.Fill;

            commentsPanel.AutoScroll =
            true;

            commentsPanel.FlowDirection =
            FlowDirection.TopDown;

            commentsPanel.WrapContents =
            false;

            commentsPanel.Resize +=
            new EventHandler(
                Panels_LayoutChanged);

            commentsPanel.Scroll +=
            new ScrollEventHandler(
                CommentsPanel_Scroll);

            commentsTab.Controls.Add(
                commentsPanel);

            rightTabs.TabPages.Add(
                postTab);

            rightTabs.TabPages.Add(
                commentsTab);

            split.Panel2.Controls.Add(
                rightTabs);

            Controls.Add(
                statusFooter);

            Controls.Add(
                newPostPanel);

            Controls.Add(
                searchPopupPanel);

            Controls.Add(
                split);

            Controls.Add(
                topBarPanel);

            Click +=
            new EventHandler(
                NonSearchSurface_Click);

            uiBuilt = true;

            UpdateAccountStatusLabel();
            RefreshAccountMenuState();
            UpdateNewPostVisibility();
            RefreshDrawerContents();
            RefreshSearchSuggestions("");
            UpdateTopBarLayout();
            UpdateDrawerLayout();
            UpdatePostLayoutMetrics();
            Panels_LayoutChanged(
                this,
                EventArgs.Empty);

            scrollPollTimer =
            new System.Windows.Forms.Timer();

            scrollPollTimer.Interval =
            150;

            scrollPollTimer.Tick +=
            new EventHandler(
                ScrollPollTimer_Tick);

            scrollPollTimer.Start();
        }

        private void BuildMenu()
        {
            mainMenu =
            new MainMenu();

            accountMenu =
            new MenuItem("&Account");

            useSavedAccountMenuItem =
            new MenuItem(
                "Use &Saved Account",
                new EventHandler(
                    UseSavedAccountMenuItem_Click));

            loginMenuItem =
            new MenuItem(
                "&Login...",
                new EventHandler(
                    LoginMenuItem_Click));

            anonymousMenuItem =
            new MenuItem(
                "&Anonymous Mode",
                new EventHandler(
                    AnonymousMenuItem_Click));

            accountMenu.MenuItems.Add(
                useSavedAccountMenuItem);
            accountMenu.MenuItems.Add(
                loginMenuItem);
            accountMenu.MenuItems.Add(
                anonymousMenuItem);

            mainMenu.MenuItems.Add(
                accountMenu);

            Menu = mainMenu;
        }

        private void BuildTopNavigation()
        {
            topNavButtonsPanel.Controls.Clear();
            feedCategories.Clear();

            AddFeedCategory(
                "Home",
                "best",
                0);

            AddFeedCategory(
                "Popular",
                "r/popular/hot",
                88);

            AddFeedCategory(
                "News",
                "r/news/hot",
                176);

            AddFeedCategory(
                "Explore",
                "r/all",
                264);

            UpdateActiveFeedButton();
        }

        private void BuildDrawerPanel()
        {
            drawerPanel.Controls.Clear();

            drawerHeaderPanel =
            new Panel();

            drawerHeaderPanel.Dock =
            DockStyle.Top;

            drawerHeaderPanel.Height =
            28;

            drawerHeaderLabel =
            new Label();

            drawerHeaderLabel.Text =
            "Topics";

            drawerHeaderLabel.Left =
            8;

            drawerHeaderLabel.Top =
            7;

            drawerHeaderLabel.Width =
            180;

            drawerCloseButton =
            new Button();

            drawerCloseButton.Text =
            "<";

            drawerCloseButton.Width =
            18;

            drawerCloseButton.Height =
            60;

            drawerCloseButton.Left =
            0;

            drawerCloseButton.Top =
            10;

            drawerCloseButton.Click +=
            new EventHandler(
                DrawerCloseButton_Click);

            drawerListPanel =
            new Panel();

            drawerListPanel.Dock =
            DockStyle.Fill;

            drawerListPanel.AutoScroll =
            true;

            drawerListPanel.Resize +=
            new EventHandler(
                DrawerListPanel_Resize);

            drawerHeaderPanel.Controls.Add(
                drawerHeaderLabel);

            drawerPanel.Controls.Add(
                drawerCloseButton);

            drawerPanel.Controls.Add(
                drawerListPanel);

            drawerPanel.Controls.Add(
                drawerHeaderPanel);
        }

        private void AddFeedCategory(
            string label,
            string feedName,
            int left)
        {
            Button button =
            new Button();

            button.Text = label;
            button.Left = left;
            button.Top = 0;
            button.Width = 84;
            button.Height = 30;
            button.Tag = feedName;
            button.Click +=
            new EventHandler(
                FeedCategoryButton_Click);

            topNavButtonsPanel.Controls.Add(
                button);

            FeedCategoryInfo info =
            new FeedCategoryInfo();

            info.Label = label;
            info.FeedName = feedName;
            info.Button = button;

            feedCategories.Add(info);
        }

        private void DrawerListPanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdateDrawerButtonWidthsOnly();
        }

        private void TopBarPanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdateTopBarLayout();
        }

        private void UpdateTopBarLayout()
        {
            if (topBarPanel == null)
                return;

            int searchWidth =
            320;

            if (topBarPanel.ClientSize.Width <
                860)
                searchWidth = 280;

            int searchRightMargin =
            12;

            int searchLeft =
            topBarPanel.ClientSize.Width -
            searchWidth -
            searchRightMargin;

            int minSearchLeft =
            topNavButtonsPanel.Right + 12;

            if (searchLeft < minSearchLeft)
            {
                searchLeft =
                minSearchLeft;

                searchWidth =
                topBarPanel.ClientSize.Width -
                searchRightMargin -
                searchLeft;
            }

            if (searchWidth < 180)
                searchWidth = 180;

            searchHostPanel.Width =
            searchWidth;

            searchHostPanel.Left =
            searchLeft;

            searchTextBox.Width =
            searchHostPanel.ClientSize.Width - 60;

            searchGoButton.Left =
            searchTextBox.Right + 4;

            searchPopupPanel.Width =
            searchHostPanel.Width;

            Point popupLocation =
            PointToClient(
                searchHostPanel.Parent.PointToScreen(
                    new Point(
                        searchHostPanel.Left,
                        searchHostPanel.Bottom + 2)));

            searchPopupPanel.Left =
            popupLocation.X;

            searchPopupPanel.Top =
            popupLocation.Y;

            if (searchPopupPanel.Right >
                ClientSize.Width - 8)
                searchPopupPanel.Left =
                ClientSize.Width -
                searchPopupPanel.Width - 8;

            newPostButton.Left =
            (newPostPanel.ClientSize.Width -
             newPostButton.Width) / 2;

            if (newPostButton.Left < 8)
                newPostButton.Left = 8;
        }

        private void DrawerHandlePanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdateDrawerLayout();
        }

        private void UpdateDrawerLayout()
        {
            if (feedHostPanel == null ||
                drawerPanel == null ||
                drawerOpenButton == null ||
                drawerHandlePanel == null)
                return;

            drawerPanel.Height =
            feedHostPanel.ClientSize.Height;

            if (drawerCloseButton != null)
            {
                drawerCloseButton.Left =
                drawerPanel.ClientSize.Width -
                drawerCloseButton.Width;

                drawerCloseButton.Top =
                (drawerPanel.ClientSize.Height -
                 drawerCloseButton.Height) / 2;

                if (drawerCloseButton.Top < 8)
                    drawerCloseButton.Top = 8;

                drawerCloseButton.BringToFront();
            }

            drawerOpenButton.Left =
            (drawerHandlePanel.ClientSize.Width -
             drawerOpenButton.Width) / 2;

            if (drawerOpenButton.Left < 0)
                drawerOpenButton.Left = 0;

            drawerOpenButton.Top =
            (drawerHandlePanel.ClientSize.Height -
             drawerOpenButton.Height) / 2;

            if (drawerOpenButton.Top < 8)
                drawerOpenButton.Top = 8;
        }

        private void DrawerOpenButton_Click(
            object sender,
            EventArgs e)
        {
            SetDrawerOpen(
                true);
        }

        private void DrawerCloseButton_Click(
            object sender,
            EventArgs e)
        {
            SetDrawerOpen(
                false);
        }

        private void SetDrawerOpen(
            bool open)
        {
            if (drawerPanel != null)
                drawerPanel.Visible = open;

            if (drawerOpenButton != null)
                drawerOpenButton.Visible = !open;

            if (open)
                HideSearchPopup();

            if (open)
                SetStatusText(
                    "Topics drawer opened.");
        }

        private void NewPostPanel_Resize(
            object sender,
            EventArgs e)
        {
            UpdateTopBarLayout();
        }

        private void NewPostButton_Click(
            object sender,
            EventArgs e)
        {
            MessageBox.Show(
                "Not implemented",
                "New Post",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RefreshDrawerContents()
        {
            if (drawerListPanel == null)
                return;

            drawerListPanel.Controls.Clear();

            List<RedditSubreddit> items =
            GetAvailableSubreddits();

            if (subscriptionsLoading)
            {
                Label label =
                MakeLabel(
                    "Loading subscriptions...");

                label.Left = 6;
                label.Top = 6;
                drawerListPanel.Controls.Add(
                    label);
                return;
            }

            int i;
            int widestText =
            0;
            for (i = 0;
                 i < items.Count;
                 i++)
            {
                string buttonText =
                !string.IsNullOrEmpty(
                    items[i].DisplayNamePrefixed)
                ? items[i].DisplayNamePrefixed
                : "r/" + items[i].DisplayName;

                Size measured =
                TextRenderer.MeasureText(
                    buttonText + " ",
                    Font);

                if (measured.Width > widestText)
                    widestText = measured.Width;

                Button button =
                new Button();

                button.Left = 8;
                button.Top = 8 + (i * 30);
                button.Width = 180;
                button.Height = 24;
                button.Text =
                buttonText;
                button.Tag =
                "r/" + items[i].DisplayName;
                button.Click +=
                new EventHandler(
                    DrawerSubredditButton_Click);

                drawerListPanel.Controls.Add(
                    button);
            }

            UpdateDrawerSizing(
                widestText);
        }

        private void UpdateDrawerSizing(
            int widestText)
        {
            if (drawerListPanel == null ||
                drawerPanel == null)
                return;

            int desiredWidth =
            widestText + 40;

            if (desiredWidth < 180)
                desiredWidth = 180;

            if (desiredWidth > 260)
                desiredWidth = 260;

            drawerPanel.Width =
            desiredWidth + 16;

            int width =
            drawerListPanel.ClientSize.Width - 16;

            if (width < 120)
                width = 120;

            int i;
            for (i = 0;
                 i < drawerListPanel.Controls.Count;
                 i++)
            {
                Button button =
                drawerListPanel.Controls[i]
                as Button;

                if (button != null)
                {
                    button.Width = width;
                    button.Left = 8;
                }
            }
        }

        private void UpdateDrawerButtonWidthsOnly()
        {
            if (drawerListPanel == null)
                return;

            int width =
            drawerListPanel.ClientSize.Width - 16;

            if (width < 120)
                width = 120;

            int i;
            for (i = 0;
                 i < drawerListPanel.Controls.Count;
                 i++)
            {
                Button button =
                drawerListPanel.Controls[i]
                as Button;

                if (button != null)
                {
                    button.Width = width;
                    button.Left = 8;
                }
            }
        }

        private void DrawerSubredditButton_Click(
            object sender,
            EventArgs e)
        {
            Button button =
            sender as Button;

            if (button == null)
                return;

            string feedName =
            button.Tag as string;

            if (string.IsNullOrEmpty(feedName))
                return;

            SelectFeedCategory(
                feedName,
                true);

            SetDrawerOpen(false);
            SetStatusText(
                "Opened " +
                button.Text);
        }

        private List<RedditSubreddit> GetAvailableSubreddits()
        {
            if (api.IsAuthenticated &&
                subscribedSubreddits.Count > 0)
                return subscribedSubreddits;

            return BuildFallbackSubreddits();
        }

        private List<RedditSubreddit> BuildFallbackSubreddits()
        {
            List<RedditSubreddit> result =
            new List<RedditSubreddit>();

            int i;
            for (i = 0;
                 i < FallbackSubreddits.Length;
                 i++)
            {
                RedditSubreddit subreddit =
                new RedditSubreddit();

                subreddit.DisplayName =
                FallbackSubreddits[i];

                subreddit.DisplayNamePrefixed =
                "r/" + FallbackSubreddits[i];

                result.Add(subreddit);
            }

            return result;
        }

        private void FeedCategoryButton_Click(
            object sender,
            EventArgs e)
        {
            Button button =
            sender as Button;

            if (button == null)
                return;

            string feedName =
            button.Tag as string;

            if (string.IsNullOrEmpty(feedName))
                return;

            SelectFeedCategory(
                feedName,
                true);
        }

        private void DockbarRefreshButton_Click(
            object sender,
            EventArgs e)
        {
            RefreshFeed(true);
        }

        private void SelectFeedCategory(
            string feedName,
            bool refresh)
        {
            string normalized =
            NormalizeFeedName(feedName);

            if (currentFeedName != normalized)
            {
                currentFeedName =
                normalized;
                SaveCurrentAccountState();
            }

            UpdateActiveFeedButton();
            UpdateNewPostVisibility();

            if (refresh)
                RefreshFeed(true);
        }

        private void UpdateActiveFeedButton()
        {
            int i;

            for (i = 0;
                 i < feedCategories.Count;
                 i++)
            {
                FeedCategoryInfo info =
                feedCategories[i];

                if (info.Button == null)
                    continue;

                if (string.Equals(
                        info.FeedName,
                        currentFeedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    info.Button.BackColor =
                    SystemColors.ControlDark;
                    info.Button.ForeColor =
                    SystemColors.ControlLightLight;
                }
                else
                {
                    info.Button.BackColor =
                    SystemColors.Control;
                    info.Button.ForeColor =
                    SystemColors.ControlText;
                }
            }
        }

        private string GetCurrentFeedLabel()
        {
            int i;

            for (i = 0;
                 i < feedCategories.Count;
                 i++)
            {
                if (string.Equals(
                        feedCategories[i].FeedName,
                        currentFeedName,
                        StringComparison.OrdinalIgnoreCase))
                    return feedCategories[i].Label;
            }

            if (currentFeedName != null &&
                currentFeedName.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
                return "Search";

            if (currentFeedName != null &&
                currentFeedName.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
                return currentFeedName;

            return "Feed";
        }

        private static string NormalizeFeedName(
            string feedName)
        {
            if (string.IsNullOrEmpty(feedName))
                return "best";

            string normalized =
            feedName.Trim();

            if (string.Equals(normalized, "best", StringComparison.OrdinalIgnoreCase))
                return "best";

            if (string.Equals(normalized, "r/popular/hot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "popular", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "hot", StringComparison.OrdinalIgnoreCase))
                return "r/popular/hot";

            if (string.Equals(normalized, "r/news/hot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "news", StringComparison.OrdinalIgnoreCase))
                return "r/news/hot";

            if (string.Equals(normalized, "r/all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "explore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "top", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "new", StringComparison.OrdinalIgnoreCase))
                return "r/all";

            if (normalized.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
                return normalized;

            if (normalized.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
                return normalized;

            return "best";
        }

        private void SetStatusText(
            string text)
        {
            if (!uiBuilt || statusFooter == null)
                return;

            statusFooter.SetStatus(text);
        }

        private void SetFeedLoadingStatus()
        {
            SetStatusText(
                "Loading " +
                GetCurrentFeedLabel() +
                " posts...");
        }

        private void SetFeedLoadedStatus()
        {
            SetStatusText(
                "Posts: " +
                GetVisibleFeedCount().ToString() +
                " loaded from " +
                GetCurrentFeedLabel());
        }

        private void SetCommentsLoadingStatus()
        {
            SetStatusText(
                "Comments: loading...");
        }

        private void SetCommentsRenderedStatus(
            string suffix)
        {
            int total =
            currentCommentItems.Count;

            int rendered =
            currentCommentRenderIndex;

            if (rendered < 0)
                rendered = 0;

            if (rendered > total)
                rendered = total;

            if (total <= 0)
            {
                SetStatusText(
                    "Comments: 0 rendered");
                return;
            }

            int percent =
            (rendered * 100) / total;

            string text =
            "Comments: " +
            rendered.ToString() +
            "/" +
            total.ToString() +
            " rendered (" +
            percent.ToString() +
            "%)";

            if (!string.IsNullOrEmpty(suffix))
                text += ", " + suffix;

            SetStatusText(text);
        }

        private int GetVisibleFeedCount()
        {
            if (feedPanel == null)
                return 0;

            return feedPanel.Controls.Count;
        }

        private void UpdateNewPostVisibility()
        {
            if (!uiBuilt || newPostPanel == null)
                return;

            if (string.IsNullOrEmpty(currentFeedName))
            {
                newPostPanel.Visible = false;
                return;
            }

            bool visible =
            currentFeedName.StartsWith("r/", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentFeedName, "r/popular/hot", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentFeedName, "r/all", StringComparison.OrdinalIgnoreCase);

            newPostPanel.Visible =
            visible;
        }


        private Label MakeLabel(
            string text)
        {
            Label l =
            new Label();

            l.Text = text;
            l.AutoSize = true;
            l.Padding =
            new Padding(6);

            return l;
        }

        private delegate void UiAction();

        private void UpdateUi(
            UiAction action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
                return;
            }

            action();
        }
    }
}
