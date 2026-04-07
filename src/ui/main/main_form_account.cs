using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using ReW9x.Models;

namespace ReW9x.UI
{
    public sealed partial class MainForm
    {
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
    }
}
