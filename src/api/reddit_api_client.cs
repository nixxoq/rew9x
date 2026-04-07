using System;
using System.Collections.Generic;
using System.Globalization;

using ReW9x;
using ReW9x.Models;
using ReW9x.Utils;
using OpenSslWrp;
namespace ReW9x.Api
{
    public sealed partial class RedditApiClient
    {
        private readonly OAuthClient _oauth;
        private RedditToken _token;
        private bool _refreshing;

        public event EventHandler TokenChanged;

        public RedditApiClient(OAuthClient oauth, RedditToken token)
        {
            _oauth = oauth;
            _token = token;
        }

        public RedditToken CurrentToken
        {
            get { return _token; }
            set
            {
                _token = value;
                OnTokenChanged();
            }
        }

        public bool IsAuthenticated
        {
            get { return _token != null && _token.HasAccessToken; }
        }

        public void EnsureValidToken()
        {
            if (_token == null)
                return;

            if (!_token.IsExpiredSoon)
                return;

            if (!_token.HasRefreshToken || _refreshing)
                return;

            try
            {
                _refreshing = true;
                RedditToken refreshed = _oauth.RefreshToken(_token.RefreshToken);
                if (refreshed != null && refreshed.HasAccessToken)
                {
                    if (string.IsNullOrEmpty(refreshed.RefreshToken))
                        refreshed.RefreshToken = _token.RefreshToken;
                    _token = refreshed;
                    OnTokenChanged();
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        public byte[] DownloadBytes(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int attempt;
            for (attempt = 0; attempt < 2; attempt++)
            {
                EnsureValidToken();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = _oauth.UserAgentValue;
                headers["Accept"] = "image/*,*/*;q=0.8";
                headers["Accept-Language"] = "en-US,en;q=0.8";

                if (IsAuthenticated && _token != null && !string.IsNullOrEmpty(_token.AccessToken))
                    headers["Authorization"] = "bearer " + _token.AccessToken;

                OpenSslWrp.HttpResponse resp =
                OpenSslWrp.HttpsClient.Get(url, headers);

                if (resp.StatusCode == 401 &&
                    _token != null &&
                    _token.HasRefreshToken &&
                    attempt == 0)
                {
                    _token = _oauth.RefreshToken(_token.RefreshToken);
                    OnTokenChanged();
                    continue;
                }

                if (resp.StatusCode < 200 || resp.StatusCode >= 300)
                    throw new InvalidOperationException("HTTP " + resp.StatusCode.ToString(CultureInfo.InvariantCulture));

                return resp.Body;
            }

            throw new InvalidOperationException("Request failed.");
        }

        private string BuildApiUrl(string path)
        {
            if (IsAuthenticated)
                return "https://oauth.reddit.com" + path;
            return "https://www.reddit.com" + path;
        }

        private string RequestJson(string method, string url, string contentType, byte[] body, bool preferAuth)
        {
            int attempt;
            for (attempt = 0; attempt < 2; attempt++)
            {
                EnsureValidToken();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = _oauth.UserAgentValue;
                headers["Accept"] = "application/json";
                headers["Accept-Language"] = "en-US,en;q=0.8";

                if (IsAuthenticated && _token != null && !string.IsNullOrEmpty(_token.AccessToken))
                    headers["Authorization"] = "bearer " + _token.AccessToken;

                if (body != null && contentType != null)
                    headers["Content-Type"] = contentType;

                OpenSslWrp.HttpResponse resp;
                if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    resp = OpenSslWrp.HttpsClient.Get(url, headers);
                else
                    resp = OpenSslWrp.HttpsClient.Request(method, url, headers, contentType, body);

                if (resp.StatusCode == 401 && _token != null && _token.HasRefreshToken && attempt == 0)
                {
                    _token = _oauth.RefreshToken(_token.RefreshToken);
                    OnTokenChanged();
                    continue;
                }

                if (resp.StatusCode < 200 || resp.StatusCode >= 300)
                    throw new InvalidOperationException("HTTP " + resp.StatusCode.ToString(CultureInfo.InvariantCulture));

                return resp.Text;
            }

            throw new InvalidOperationException("Request failed.");
        }

        private void OnTokenChanged()
        {
            if (TokenChanged != null)
                TokenChanged(this, EventArgs.Empty);
        }
    }
}
