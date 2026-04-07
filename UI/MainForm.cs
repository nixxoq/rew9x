using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Reddit98Client
{
    public sealed class MainForm : Form
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

        private string lastFeedViewportKey = "";
        private string lastCommentViewportKey = "";
        private List<FeedCategoryInfo> feedCategories =
        new List<FeedCategoryInfo>();
        private List<RedditSubreddit> subscribedSubreddits =
        new List<RedditSubreddit>();
        private List<RedditPost> searchPosts =
        new List<RedditPost>();
        private int searchRequestId = 0;

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
            Text = "Reddit for Windows 9x";
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

            Win98TlsClient.HttpsClient.Initialize(
                "cacert.pem",
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
                Win98TlsClient.HttpsClient.Shutdown();
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

            topBarPanel.Controls.Add(
                searchPopupPanel);

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
                split);

            Controls.Add(
                topBarPanel);

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

            searchPopupPanel.Left =
            searchHostPanel.Left;

            searchPopupPanel.Top =
            searchHostPanel.Bottom + 2;

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

        private void SearchTextBox_TextChanged(
            object sender,
            EventArgs e)
        {
            if (updatingSearchItems)
                return;

            BeginSearchSuggestions(
                searchTextBox.Text);

            RefreshSearchSuggestions(
                searchTextBox.Text);
        }

        private void SearchTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ApplySelectedSearchSuggestion();
            }
        }

        private void SearchGoButton_Click(
            object sender,
            EventArgs e)
        {
            ApplySelectedSearchSuggestion();
        }

        private void ApplySelectedSearchSuggestion()
        {
            SearchSuggestionItem suggestion =
            null;

            if (searchPopupResultsPanel != null &&
                searchPopupResultsPanel.Controls.Count > 0)
            {
                int i;
                for (i = 0;
                     i < searchPopupResultsPanel.Controls.Count;
                     i++)
                {
                    Button button =
                    searchPopupResultsPanel.Controls[i]
                    as Button;

                    if (button != null)
                    {
                        suggestion =
                        button.Tag as SearchSuggestionItem;
                        break;
                    }
                }
            }

            if (suggestion != null)
            {
                ApplySearchSuggestion(
                    suggestion);
                return;
            }

            NavigateToSearchText(
                searchTextBox.Text);
        }

        private void NavigateToSearchText(
            string text)
        {
            string normalizedText =
            NormalizeSubredditQuery(text);

            if (string.IsNullOrEmpty(
                    normalizedText))
                return;

            SelectFeedCategory(
                "r/" + normalizedText,
                true);

            searchTextBox.Text =
            "r/" + normalizedText;

            HideSearchPopup();
            SetDrawerOpen(false);
            SetStatusText(
                "Opened r/" +
                normalizedText);
        }

        private void ApplySearchSuggestion(
            SearchSuggestionItem suggestion)
        {
            if (suggestion == null)
                return;

            if (suggestion.IsSubreddit)
            {
                SelectFeedCategory(
                    suggestion.FeedName,
                    true);

                searchTextBox.Text =
                suggestion.DisplayText;

                HideSearchPopup();
                SetDrawerOpen(false);
                SetStatusText(
                    "Opened " +
                    suggestion.DisplayText);

                return;
            }

            SelectFeedCategory(
                suggestion.FeedName,
                true);

            searchTextBox.Text =
            suggestion.QueryText;

            HideSearchPopup();
            SetStatusText(
                "Search results for \"" +
                suggestion.QueryText +
                "\"");
        }

        private void BeginSearchSuggestions(
            string query)
        {
            string normalizedQuery =
            NormalizeSubredditQuery(query);

            if (string.IsNullOrEmpty(normalizedQuery) ||
                normalizedQuery.Length < 2)
            {
                searchLoading = false;
                searchPosts.Clear();
                HideSearchPopup();
                return;
            }

            searchRequestId++;
            int requestId =
            searchRequestId;

            searchLoading = true;

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    RedditListingPage page =
                    api.SearchPosts(
                        normalizedQuery,
                        null,
                        5);

                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            searchRequestId)
                            return;

                        searchPosts =
                        page.Posts;

                        searchLoading =
                        false;

                        RefreshSearchSuggestions(
                            searchTextBox.Text);
                    });
                }
                catch
                {
                    UpdateUi(delegate()
                    {
                        if (requestId !=
                            searchRequestId)
                            return;

                        searchPosts.Clear();
                        searchLoading = false;
                        RefreshSearchSuggestions(
                            searchTextBox.Text);
                    });
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void RefreshSearchSuggestions(
            string query)
        {
            if (searchTextBox == null ||
                searchPopupResultsPanel == null ||
                searchPopupPanel == null)
                return;

            updatingSearchItems = true;
            List<SearchSuggestionItem> suggestions =
            BuildSearchSuggestions(query);

            RenderSearchPopup(
                suggestions,
                query);
            updatingSearchItems = false;
        }

        private List<SearchSuggestionItem> BuildSearchSuggestions(
            string query)
        {
            List<SearchSuggestionItem> result =
            new List<SearchSuggestionItem>();

            List<RedditSubreddit> source =
            GetAvailableSubreddits();

            string normalizedQuery =
            NormalizeSubredditQuery(query);

            int i;
            for (i = 0;
                 i < source.Count;
                 i++)
            {
                RedditSubreddit subreddit =
                source[i];

                string display =
                !string.IsNullOrEmpty(
                    subreddit.DisplayNamePrefixed)
                ? subreddit.DisplayNamePrefixed
                : "r/" + subreddit.DisplayName;

                if (!string.IsNullOrEmpty(normalizedQuery))
                {
                    string haystack =
                    display.ToLowerInvariant();

                    if (!string.IsNullOrEmpty(
                            subreddit.Title))
                        haystack += " " +
                        subreddit.Title.ToLowerInvariant();

                    if (haystack.IndexOf(
                            normalizedQuery.ToLowerInvariant()) < 0)
                        continue;
                }

                SearchSuggestionItem item =
                new SearchSuggestionItem();

                item.IsSubreddit =
                true;

                item.FeedName =
                "r/" + subreddit.DisplayName;

                item.DisplayText =
                display;

                item.Section =
                "Communities";

                result.Add(item);

                if (result.Count >= 12)
                    break;
            }

            if (!string.IsNullOrEmpty(normalizedQuery) &&
                normalizedQuery.Length >= 2)
            {
                int j;
                for (j = 0;
                     j < searchPosts.Count && j < 5;
                     j++)
                {
                    SearchSuggestionItem item =
                    new SearchSuggestionItem();

                    item.IsSubreddit =
                    false;

                    item.QueryText =
                    normalizedQuery;

                    item.FeedName =
                    "search:" + normalizedQuery;

                    item.DisplayText =
                    searchPosts[j].Title;

                    item.Section =
                    "Search results";

                    result.Add(item);
                }
            }

            return result;
        }

        private void RenderSearchPopup(
            List<SearchSuggestionItem> suggestions,
            string query)
        {
            searchPopupResultsPanel.Controls.Clear();

            if (suggestions == null ||
                suggestions.Count == 0)
            {
                HideSearchPopup();
                return;
            }

            int top = 6;
            string currentSection = "";
            int i;

            for (i = 0;
                 i < suggestions.Count;
                 i++)
            {
                SearchSuggestionItem item =
                suggestions[i];

                if (!string.Equals(
                        currentSection,
                        item.Section,
                        StringComparison.Ordinal))
                {
                    currentSection =
                    item.Section;

                    Label header =
                    new Label();

                    header.Left = 8;
                    header.Top = top;
                    header.Width = searchPopupResultsPanel.ClientSize.Width - 16;
                    header.Height = 18;
                    header.Text = currentSection;

                    searchPopupResultsPanel.Controls.Add(
                        header);

                    top += 20;
                }

                Button button =
                new Button();

                button.Left = 8;
                button.Top = top;
                button.Width = searchPopupResultsPanel.ClientSize.Width - 24;
                button.Height = 24;
                button.Text = item.DisplayText;
                button.Tag = item;
                button.Click +=
                new EventHandler(
                    SearchSuggestionButton_Click);

                searchPopupResultsPanel.Controls.Add(
                    button);

                top += 28;
            }

            if (searchLoading &&
                !string.IsNullOrEmpty(
                    NormalizeSubredditQuery(query)))
            {
                Label loading =
                new Label();

                loading.Left = 8;
                loading.Top = top;
                loading.Width = searchPopupResultsPanel.ClientSize.Width - 16;
                loading.Height = 18;
                loading.Text = "Searching...";

                searchPopupResultsPanel.Controls.Add(
                    loading);

                top += 20;
            }

            searchPopupPanel.Visible = true;
            searchPopupPanel.BringToFront();
        }

        private void SearchSuggestionButton_Click(
            object sender,
            EventArgs e)
        {
            Button button =
            sender as Button;

            if (button == null)
                return;

            SearchSuggestionItem suggestion =
            button.Tag as SearchSuggestionItem;

            ApplySearchSuggestion(
                suggestion);
        }

        private void HideSearchPopup()
        {
            if (searchPopupPanel != null)
                searchPopupPanel.Visible = false;
        }

        private static string NormalizeSubredditQuery(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            string value =
            text.Trim();

            if (value.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2);

            if (value.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(1);

            int slash =
            value.IndexOf('/');

            if (slash >= 0)
                value = value.Substring(0, slash);

            return value.Trim();
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
            for (i = 0;
                 i < items.Count;
                 i++)
            {
                Button button =
                new Button();

                button.Left = 8;
                button.Top = 8 + (i * 30);
                button.Width = 180;
                button.Height = 24;
                button.Text =
                !string.IsNullOrEmpty(
                    items[i].DisplayNamePrefixed)
                ? items[i].DisplayNamePrefixed
                : "r/" + items[i].DisplayName;
                button.Tag =
                "r/" + items[i].DisplayName;
                button.Click +=
                new EventHandler(
                    DrawerSubredditButton_Click);

                drawerListPanel.Controls.Add(
                    button);
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

        private void MainForm_Shown(
            object sender,
            EventArgs e)
        {
            if (!HasSavedAccount())
                ShowStartupModeChoice();
            else
            {
                BeginRefreshCurrentUser();
                BeginLoadSubscribedSubreddits();
            }

            RefreshFeed(true);
        }

        private void Api_TokenChanged(
            object sender,
            EventArgs e)
        {
            SaveCurrentAccountState();

            UpdateUi(delegate()
            {
                RefreshAccountMenuState();
                UpdateAccountStatusLabel();
                RefreshDrawerContents();
                RefreshSearchSuggestions(
                    searchTextBox != null
                    ? searchTextBox.Text
                    : "");
            });
        }

        private void ShowStartupModeChoice()
        {
            StartupModeForm dialog =
            new StartupModeForm();

            DialogResult result =
            dialog.ShowDialog(this);

            if (result != DialogResult.OK)
            {
                EnterAnonymousMode(false);
                return;
            }

            if (dialog.Choice ==
                StartupModeChoice.Login)
            {
                RunLoginFlow();
                return;
            }

            EnterAnonymousMode(false);
        }

        private void RunLoginFlow()
        {
            LoginForm dialog =
            new LoginForm(oauthClient);

            if (dialog.ShowDialog(this) !=
                DialogResult.OK)
                return;

            api.CurrentToken =
            dialog.TokenResult;

            currentUsername = null;

            SaveCurrentAccountState();
            UpdateAccountStatusLabel();
            SetStatusText(
                "Logged in.");

            RefreshAccountMenuState();
            BeginRefreshCurrentUser();
            BeginLoadSubscribedSubreddits();
            RefreshFeed(true);
        }

        private void EnterAnonymousMode(
            bool refreshFeed)
        {
            api.CurrentToken = null;
            currentUsername = null;
            subscribedSubreddits.Clear();
            subscriptionsLoading = false;

            UpdateAccountStatusLabel();
            RefreshAccountMenuState();
            RefreshDrawerContents();
            RefreshSearchSuggestions(
                searchTextBox != null
                ? searchTextBox.Text
                : "");

            SetStatusText(
                "Anonymous mode.");

            if (refreshFeed)
                RefreshFeed(true);
        }

        private void UseSavedAccount()
        {
            if (!HasSavedAccount())
                return;

            api.CurrentToken =
            savedAccountState.ToToken();

            currentUsername =
            savedAccountState.RedditUsername;

            UpdateAccountStatusLabel();
            RefreshAccountMenuState();

            SetStatusText(
                "Using saved account.");

            BeginRefreshCurrentUser();
            BeginLoadSubscribedSubreddits();
            RefreshFeed(true);
        }

        private void UseSavedAccountMenuItem_Click(
            object sender,
            EventArgs e)
        {
            UseSavedAccount();
        }

        private void LoginMenuItem_Click(
            object sender,
            EventArgs e)
        {
            RunLoginFlow();
        }

        private void AnonymousMenuItem_Click(
            object sender,
            EventArgs e)
        {
            EnterAnonymousMode(true);
        }

        private bool HasSavedAccount()
        {
            return
            savedAccountState != null &&
            savedAccountState.ToToken() != null &&
            savedAccountState.ToToken().HasRefreshToken;
        }

        private void RefreshAccountMenuState()
        {
            if (!uiBuilt)
                return;

            useSavedAccountMenuItem.Enabled =
            HasSavedAccount();
        }

        private void UpdateAccountStatusLabel()
        {
            if (!uiBuilt)
                return;

            if (api.IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(
                    currentUsername))
                {
                    statusFooter.SetMode(
                        "Account: u/" +
                        currentUsername);
                }
                else
                {
                    statusFooter.SetMode(
                        "Account: signed in");
                }

                return;
            }

            statusFooter.SetMode(
                "Mode: anonymous");
        }

        private void BeginRefreshCurrentUser()
        {
            if (!api.IsAuthenticated)
                return;

            Thread t =
            new Thread(delegate()
            {
                try
                {
                    RedditUser user =
                    api.GetCurrentUser();

                    UpdateUi(delegate()
                    {
                        currentUsername =
                        user.Name;

                        SaveCurrentAccountState();
                        UpdateAccountStatusLabel();
                    });
                }
                catch
                {
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void BeginLoadSubscribedSubreddits()
        {
            if (!api.IsAuthenticated)
            {
                subscribedSubreddits.Clear();
                subscriptionsLoading = false;
                RefreshDrawerContents();
                RefreshSearchSuggestions(
                    searchTextBox != null
                    ? searchTextBox.Text
                    : "");
                return;
            }

            if (subscriptionsLoading)
                return;

            subscriptionsLoading = true;
            RefreshDrawerContents();

            Thread t =
            new Thread(new ThreadStart(delegate()
            {
                try
                {
                    List<RedditSubreddit> loaded =
                    api.LoadSubscribedSubreddits(100);

                    UpdateUi(delegate()
                    {
                        subscribedSubreddits =
                        loaded;

                        subscriptionsLoading =
                        false;

                        RefreshDrawerContents();
                        RefreshSearchSuggestions(
                            searchTextBox != null
                            ? searchTextBox.Text
                            : "");

                        SetStatusText(
                            "Loaded " +
                            loaded.Count.ToString() +
                            " subscriptions.");
                    });
                }
                catch (Exception ex)
                {
                    UpdateUi(delegate()
                    {
                        subscriptionsLoading =
                        false;

                        subscribedSubreddits.Clear();
                        RefreshDrawerContents();
                        RefreshSearchSuggestions(
                            searchTextBox != null
                            ? searchTextBox.Text
                            : "");

                        SetStatusText(
                            ex.Message);
                    });
                }
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void SaveCurrentAccountState()
        {
            AccountState state =
            savedAccountState != null
            ? CloneAccountState(
                savedAccountState)
            : new AccountState();

            if (api.CurrentToken != null)
            {
                state =
                AccountState.FromToken(
                    api.CurrentToken);

                state.RedditUsername =
                currentUsername;
            }

            state.SelectedFeed =
            currentFeedName;

            accountStore.Save(state);
            savedAccountState = state;
        }

        private static AccountState CloneAccountState(
            AccountState state)
        {
            AccountState copy =
            new AccountState();

            copy.AccessToken =
            state.AccessToken;
            copy.RefreshToken =
            state.RefreshToken;
            copy.TokenType =
            state.TokenType;
            copy.Scope =
            state.Scope;
            copy.ExpiresAtUtcTicks =
            state.ExpiresAtUtcTicks;
            copy.RedditUsername =
            state.RedditUsername;
            copy.SelectedFeed =
            state.SelectedFeed;
            copy.LastSubreddit =
            state.LastSubreddit;
            copy.LastLoginUtcTicks =
            state.LastLoginUtcTicks;

            return copy;
        }

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
