using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Reddit98Client
{
    public sealed class OAuthClient
    {
        private readonly string _clientId;
        private readonly string _redirectUri;
        private readonly string _scope;
        private readonly string _userAgent;

        public OAuthClient(string clientId, string redirectUri, string scope, string userAgent)
        {
            _clientId = clientId;
            _redirectUri = redirectUri;
            _scope = scope;
            _userAgent = userAgent;
        }

        public string BuildAuthorizeUrl(string state)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("https://www.reddit.com/api/v1/authorize?");
            AppendParam(sb, "client_id", _clientId);
            AppendParam(sb, "response_type", "code");
            AppendParam(sb, "state", state);
            AppendParam(sb, "redirect_uri", _redirectUri);
            AppendParam(sb, "duration", "permanent");
            AppendParam(sb, "scope", _scope);
            TrimTrailingAmp(sb);
            return sb.ToString();
        }

        public bool TryParseCallbackUrl(string url, out string code, out string state)
        {
            code = null;
            state = null;

            if (string.IsNullOrEmpty(url))
                return false;

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch
            {
                return false;
            }

            string query = uri.Query;
            if (string.IsNullOrEmpty(query))
                return false;

            Dictionary<string, string> map = ParseQuery(query);
            if (map.ContainsKey("error"))
                throw new InvalidOperationException("OAuth error: " + map["error"]);

            if (map.ContainsKey("code"))
                code = map["code"];
            if (map.ContainsKey("state"))
                state = map["state"];

            return !string.IsNullOrEmpty(code);
        }

        public RedditToken ExchangeCodeForToken(string code)
        {
            Dictionary<string, string> headers = BuildTokenHeaders();
            string form = "grant_type=authorization_code&code=" + UrlEncode(code) + "&redirect_uri=" + UrlEncode(_redirectUri);
            Win98TlsClient.HttpResponse resp = Win98TlsClient.HttpsClient.PostForm("https://www.reddit.com/api/v1/access_token", headers, form);
            return ParseTokenResponse(resp.Text);
        }

        public RedditToken RefreshToken(string refreshToken)
        {
            Dictionary<string, string> headers = BuildTokenHeaders();
            string form = "grant_type=refresh_token&refresh_token=" + UrlEncode(refreshToken);
            Win98TlsClient.HttpResponse resp = Win98TlsClient.HttpsClient.PostForm("https://www.reddit.com/api/v1/access_token", headers, form);
            return ParseTokenResponse(resp.Text);
        }

        public string UserAgentValue
        {
            get { return _userAgent; }
        }

        public string RedirectUriValue
        {
            get { return _redirectUri; }
        }

        public string ClientIdValue
        {
            get { return _clientId; }
        }

        private Dictionary<string, string> BuildTokenHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["User-Agent"] = _userAgent;
            headers["Authorization"] = BuildBasicAuthHeader();
            headers["Accept"] = "application/json";
            return headers;
        }

        private RedditToken ParseTokenResponse(string json)
        {
            object root = JsonParser.Parse(json);
            Hashtable obj = JsonParser.AsObject(root);
            if (obj == null)
                throw new InvalidOperationException("Invalid token response.");

            RedditToken token = new RedditToken();
            token.AccessToken = JsonParser.GetString(obj, "access_token");
            token.RefreshToken = JsonParser.GetString(obj, "refresh_token");
            token.TokenType = JsonParser.GetString(obj, "token_type");
            token.Scope = JsonParser.GetString(obj, "scope");

            double expiresIn = JsonParser.GetDouble(obj, "expires_in");
            if (expiresIn <= 0)
                expiresIn = 3600.0;

            token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
            return token;
        }

        private string BuildBasicAuthHeader()
        {
            string raw = _clientId + ":";
            string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));
            return "Basic " + b64;
        }

        private static void AppendParam(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '?')
                sb.Append('&');
            sb.Append(UrlEncode(key));
            sb.Append('=');
            sb.Append(UrlEncode(value));
        }

        private static void TrimTrailingAmp(StringBuilder sb)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] == '&')
                sb.Length = sb.Length - 1;
        }

        private static string UrlEncode(string value)
        {
            if (value == null) return "";
            return Uri.EscapeDataString(value);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            string q = query;
            if (q.StartsWith("?")) q = q.Substring(1);

            string[] parts = q.Split('&');
            int i;
            for (i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part)) continue;
                int pos = part.IndexOf('=');
                string key;
                string val;
                if (pos >= 0)
                {
                    key = part.Substring(0, pos);
                    val = part.Substring(pos + 1);
                }
                else
                {
                    key = part;
                    val = "";
                }
                key = Uri.UnescapeDataString(key.Replace('+', ' '));
                val = Uri.UnescapeDataString(val.Replace('+', ' '));
                map[key] = val;
            }
            return map;
        }
    }
}
