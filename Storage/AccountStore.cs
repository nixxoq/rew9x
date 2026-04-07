using System;
using System.IO;
using System.Text;

namespace Reddit98Client
{
    public sealed class AccountStore
    {
        private readonly string _path;

        public AccountStore(string path)
        {
            _path = path;
        }

        public AccountState Load()
        {
            if (!File.Exists(_path))
                return null;

            string json = File.ReadAllText(_path, Encoding.UTF8);
            object root = JsonParser.Parse(json);
            System.Collections.Hashtable obj = JsonParser.AsObject(root);
            if (obj == null)
                return null;

            AccountState s = new AccountState();
            s.AccessToken = JsonParser.GetString(obj, "access_token");
            s.RefreshToken = JsonParser.GetString(obj, "refresh_token");
            s.TokenType = JsonParser.GetString(obj, "token_type");
            s.Scope = JsonParser.GetString(obj, "scope");
            s.ExpiresAtUtcTicks = (long)JsonParser.GetDouble(obj, "expires_at_utc_ticks");
            s.RedditUsername = JsonParser.GetString(obj, "reddit_username");
            s.SelectedFeed = JsonParser.GetString(obj, "selected_feed");
            s.LastSubreddit = JsonParser.GetString(obj, "last_subreddit");
            s.LastLoginUtcTicks = (long)JsonParser.GetDouble(obj, "last_login_utc_ticks");
            return s;
        }

        public void Save(AccountState state)
        {
            if (state == null)
                return;

            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, ToJson(state), Encoding.UTF8);
        }

        public void Clear()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        public string PathValue
        {
            get { return _path; }
        }

        private static string ToJson(AccountState s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            AppendString(sb, "access_token", s.AccessToken);
            AppendString(sb, "refresh_token", s.RefreshToken);
            AppendString(sb, "token_type", s.TokenType);
            AppendString(sb, "scope", s.Scope);
            AppendNumber(sb, "expires_at_utc_ticks", s.ExpiresAtUtcTicks);
            AppendString(sb, "reddit_username", s.RedditUsername);
            AppendString(sb, "selected_feed", s.SelectedFeed);
            AppendString(sb, "last_subreddit", s.LastSubreddit);
            AppendNumber(sb, "last_login_utc_ticks", s.LastLoginUtcTicks);

            if (sb.Length > 1 && sb[sb.Length - 1] == ',')
                sb.Length = sb.Length - 1;

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append("\"");
            sb.Append(Escape(key));
            sb.Append("\":");
            if (value == null)
            {
                sb.Append("null,");
                return;
            }
            sb.Append("\"");
            sb.Append(Escape(value));
            sb.Append("\",");
        }

        private static void AppendNumber(StringBuilder sb, string key, long value)
        {
            sb.Append("\"");
            sb.Append(Escape(key));
            sb.Append("\":");
            sb.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",");
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
