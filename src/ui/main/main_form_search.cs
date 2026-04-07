using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using ReW9x.Models;

namespace ReW9x.UI
{
    public sealed partial class MainForm
    {
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

        private void SearchTextBox_Enter(
            object sender,
            EventArgs e)
        {
            searchHasFocus = true;
            UpdateSearchPopupVisibility();
        }

        private void SearchTextBox_Leave(
            object sender,
            EventArgs e)
        {
            searchHasFocus = false;
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
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                HideSearchPopup();
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
            currentSearchSuggestions.Count > 0
            ? currentSearchSuggestions[0]
            : null;

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
                subredditSearchLoading = false;
                searchPosts.Clear();
                subredditSearchResults.Clear();
                currentSearchError = null;
                lastSearchQueryRequested = "";
                lastSubredditSearchQueryRequested = "";
                HideSearchPopup();
                return;
            }

            if (!subredditSearchLoading &&
                string.Equals(
                    lastSubredditSearchQueryRequested,
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase) &&
                !searchLoading &&
                string.Equals(
                    lastSearchQueryRequested,
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            subredditSearchRequestId++;
            int subredditRequestId =
            subredditSearchRequestId;

            searchRequestId++;
            int requestId =
            searchRequestId;

            subredditSearchLoading = true;
            searchLoading = true;
            currentSearchError = null;
            lastSearchQueryRequested = normalizedQuery;
            lastSubredditSearchQueryRequested = normalizedQuery;

            Thread subredditThread =
            new Thread(delegate()
            {
                try
                {
                    List<RedditSubreddit> matches =
                    api.SearchSubreddits(
                        normalizedQuery,
                        8);

                    UpdateUi(delegate()
                    {
                        if (subredditRequestId !=
                            subredditSearchRequestId)
                            return;

                        subredditSearchResults =
                        matches;

                        subredditSearchLoading =
                        false;

                        RefreshSearchSuggestions(
                            searchTextBox.Text);
                    });
                }
                catch
                {
                    UpdateUi(delegate()
                    {
                        if (subredditRequestId !=
                            subredditSearchRequestId)
                            return;

                        subredditSearchResults.Clear();
                        subredditSearchLoading =
                        false;

                        RefreshSearchSuggestions(
                            searchTextBox.Text);
                    });
                }
            });

            subredditThread.IsBackground =
            true;

            subredditThread.Start();

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
                        currentSearchError = null;

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
                        currentSearchError =
                        "Search failed.";
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

            currentSearchSuggestions =
            suggestions;

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

            string normalizedQuery =
            NormalizeSubredditQuery(query);

            Hashtable seenCommunities =
            new Hashtable();

            List<RedditSubreddit> source =
            GetAvailableSubreddits();

            int i;
            for (i = 0;
                 i < subredditSearchResults.Count;
                 i++)
            {
                AddCommunitySuggestion(
                    result,
                    seenCommunities,
                    subredditSearchResults[i]);

                if (result.Count >= 6)
                    break;
            }

            for (i = 0;
                 i < source.Count &&
                 result.Count < 6;
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

                AddCommunitySuggestion(
                    result,
                    seenCommunities,
                    subreddit);
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

        private void AddCommunitySuggestion(
            List<SearchSuggestionItem> result,
            Hashtable seenCommunities,
            RedditSubreddit subreddit)
        {
            if (subreddit == null)
                return;

            string key =
            subreddit.DisplayName == null
            ? ""
            : subreddit.DisplayName.ToLowerInvariant();

            if (seenCommunities.ContainsKey(key))
                return;

            SearchSuggestionItem item =
            new SearchSuggestionItem();

            item.IsSubreddit =
            true;

            item.FeedName =
            "r/" + subreddit.DisplayName;

            item.DisplayText =
            !string.IsNullOrEmpty(
                subreddit.DisplayNamePrefixed)
            ? subreddit.DisplayNamePrefixed
            : "r/" + subreddit.DisplayName;

            item.Section =
            "Communities";

            result.Add(item);
            seenCommunities[key] = true;
        }

        private void RenderSearchPopup(
            List<SearchSuggestionItem> suggestions,
            string query)
        {
            searchPopupResultsPanel.Controls.Clear();

            bool showEmpty =
            !searchLoading &&
            string.IsNullOrEmpty(currentSearchError) &&
            !string.IsNullOrEmpty(
                NormalizeSubredditQuery(query));

            if ((suggestions == null ||
                suggestions.Count == 0) &&
                !showEmpty &&
                !searchLoading &&
                string.IsNullOrEmpty(
                    currentSearchError))
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

                Control row =
                CreateSearchSuggestionControl(
                    item,
                    top);

                searchPopupResultsPanel.Controls.Add(
                    row);

                top += row.Height + 4;
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

            if (!string.IsNullOrEmpty(
                    currentSearchError))
            {
                Label errorLabel =
                new Label();

                errorLabel.Left = 8;
                errorLabel.Top = top;
                errorLabel.Width = searchPopupResultsPanel.ClientSize.Width - 16;
                errorLabel.Height = 18;
                errorLabel.Text = currentSearchError;

                searchPopupResultsPanel.Controls.Add(
                    errorLabel);

                top += 20;
            }

            if (showEmpty &&
                suggestions.Count == 0)
            {
                Label emptyLabel =
                new Label();

                emptyLabel.Left = 8;
                emptyLabel.Top = top;
                emptyLabel.Width = searchPopupResultsPanel.ClientSize.Width - 16;
                emptyLabel.Height = 18;
                emptyLabel.Text = "No results.";

                searchPopupResultsPanel.Controls.Add(
                    emptyLabel);
            }

            UpdateSearchPopupVisibility();
        }

        private Control CreateSearchSuggestionControl(
            SearchSuggestionItem item,
            int top)
        {
            Panel row =
            new Panel();

            row.Left = 8;
            row.Top = top;
            row.Width = searchPopupResultsPanel.ClientSize.Width - 24;
            row.Height =
            item.IsSubreddit ? 24 : 40;
            row.Tag = item;
            row.BorderStyle =
            BorderStyle.FixedSingle;

            Label title =
            new Label();

            title.Left = 6;
            title.Top = 4;
            title.Width = row.Width - 12;
            title.Height =
            item.IsSubreddit ? 16 : 18;
            title.Text = item.DisplayText;
            title.Tag = item;

            row.Controls.Add(title);

            if (!item.IsSubreddit)
            {
                Label meta =
                new Label();

                meta.Left = 6;
                meta.Top = 21;
                meta.Width = row.Width - 12;
                meta.Height = 14;
                meta.Text =
                !string.IsNullOrEmpty(item.FeedName) &&
                item.FeedName.StartsWith("search:", StringComparison.OrdinalIgnoreCase)
                ? "Global search"
                : item.FeedName;
                meta.Tag = item;

                row.Controls.Add(meta);
            }

            HookSearchSuggestionClicks(row);

            return row;
        }

        private void HookSearchSuggestionClicks(
            Control control)
        {
            control.Click +=
            new EventHandler(
                SearchSuggestionControl_Click);

            int i;
            for (i = 0;
                 i < control.Controls.Count;
                 i++)
            {
                HookSearchSuggestionClicks(
                    control.Controls[i]);
            }
        }

        private void SearchSuggestionControl_Click(
            object sender,
            EventArgs e)
        {
            Control control =
            sender as Control;

            if (control == null)
                return;

            SearchSuggestionItem suggestion =
            control.Tag as SearchSuggestionItem;

            ApplySearchSuggestion(
                suggestion);
        }

        private void HideSearchPopup()
        {
            if (searchPopupPanel != null)
                searchPopupPanel.Visible = false;
        }

        private void NonSearchSurface_Click(
            object sender,
            EventArgs e)
        {
            searchHasFocus = false;
            HideSearchPopup();
        }

        private void UpdateSearchPopupVisibility()
        {
            if (searchPopupPanel == null ||
                searchTextBox == null)
                return;

            string normalizedQuery =
            NormalizeSubredditQuery(
                searchTextBox.Text);

            bool hasResults =
            currentSearchSuggestions.Count > 0;

            bool showEmpty =
            !searchLoading &&
            string.IsNullOrEmpty(currentSearchError) &&
            !string.IsNullOrEmpty(normalizedQuery);

            bool shouldShow =
            searchHasFocus &&
            !string.IsNullOrEmpty(normalizedQuery) &&
            (hasResults || searchLoading || !string.IsNullOrEmpty(currentSearchError) || showEmpty);

            searchPopupPanel.Visible =
            shouldShow;

            if (shouldShow)
                searchPopupPanel.BringToFront();
        }

        private void SearchPopupPanel_VisibleChanged(
            object sender,
            EventArgs e)
        {
            if (!searchPopupPanel.Visible)
                return;

            searchPopupPanel.BringToFront();
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
    }
}
