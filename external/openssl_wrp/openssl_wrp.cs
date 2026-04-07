using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace OpenSslWrp
{
    internal static class NativeTls
    {
        [DllImport("native_tls.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int tls_global_init(
            string ca_file,
            int verify_peer,
            StringBuilder errbuf,
            int errlen
        );

        [DllImport("native_tls.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void tls_global_cleanup();

        [DllImport("native_tls.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void tls_free(IntPtr p);

        [DllImport("native_tls.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int https_request(
            string method,
            string host,
            string port,
            string path,
            string content_type,
            byte[] body,
            int body_len,
            string extra_headers,
            out IntPtr out_body,
            out int out_body_len,
            out int out_status_code,
            StringBuilder errbuf,
            int errlen
        );
    }

    public sealed class HttpResponse
    {
        public int StatusCode;
        public byte[] Body;

        public string Text
        {
            get { return EncodingDetector.Decode(Body); }
        }
    }

    public static class EncodingDetector
    {
        public static string Decode(byte[] body)
        {
            Encoding enc = Detect(body);
            return enc.GetString(body);
        }

        public static Encoding Detect(byte[] body)
        {
            if (body == null || body.Length == 0)
                return Encoding.UTF8;

            if (body.Length >= 3 && body[0] == 0xEF && body[1] == 0xBB && body[2] == 0xBF)
                return Encoding.UTF8;

            if (body.Length >= 2 && body[0] == 0xFF && body[1] == 0xFE)
                return Encoding.Unicode;

            if (body.Length >= 2 && body[0] == 0xFE && body[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            int len = body.Length < 4096 ? body.Length : 4096;
            string head = Encoding.ASCII.GetString(body, 0, len).ToLowerInvariant();

            int idx = head.IndexOf("charset=");
            if (idx >= 0)
            {
                idx += 8;
                int end = idx;
                while (end < head.Length)
                {
                    char c = head[end];
                    if (c == ';' || c == '"' || c == '\'' || c == '>' || c == '\r' || c == '\n' || c == ' ')
                        break;
                    end++;
                }

                string name = head.Substring(idx, end - idx).Trim();
                name = name.Trim('"', '\'', ' ', ';');

                if (name.Length > 0)
                {
                    try
                    {
                        return Encoding.GetEncoding(name);
                    }
                    catch
                    {
                    }
                }
            }

            return Encoding.UTF8;
        }
    }

    public static class HttpsClient
    {
        private static bool _inited;

        public static void Initialize(string caBundlePath, bool verifyPeer)
        {
            if (_inited) return;

            StringBuilder err = new StringBuilder(512);
            int ok = NativeTls.tls_global_init(caBundlePath, verifyPeer ? 1 : 0, err, err.Capacity);
            if (ok == 0)
                throw new InvalidOperationException(err.ToString());

            _inited = true;
        }

        public static HttpResponse Request(
            string method,
            string url,
            IDictionary<string, string> headers,
            string contentType,
            byte[] body
        )
        {
            if (!_inited)
                throw new InvalidOperationException("Call Initialize() first.");

            Uri uri = new Uri(url);
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only https:// URLs are supported.", "url");

            string host = uri.Host;
            string port = uri.Port > 0 ? uri.Port.ToString() : "443";
            string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

            StringBuilder extra = new StringBuilder();
            bool hasUserAgent = false;

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> kv in headers)
                {
                    if (kv.Key != null && kv.Key.ToLowerInvariant() == "user-agent")
                        hasUserAgent = true;

                    extra.Append(kv.Key);
                    extra.Append(": ");
                    extra.Append(kv.Value);
                    extra.Append("\r\n");
                }
            }

            if (!hasUserAgent)
            {
                extra.Append("User-Agent: ");
                extra.Append(ReW9x.AppConfig.UserAgent);
                extra.Append("\r\n");
            }

            if (headers == null || !ContainsHeader(headers, "Accept"))
                extra.Append("Accept: */*\r\n");

            if (headers == null || !ContainsHeader(headers, "Accept-Encoding"))
                extra.Append("Accept-Encoding: identity\r\n");

            StringBuilder err = new StringBuilder(1024);
            IntPtr bodyPtr = IntPtr.Zero;
            int outLen = 0;
            int status = 0;

            int rc = NativeTls.https_request(
                method,
                host,
                port,
                path,
                contentType,
                body,
                body == null ? 0 : body.Length,
                extra.ToString(),
                out bodyPtr,
                out outLen,
                out status,
                err,
                err.Capacity
            );

            if (rc == 0)
                throw new InvalidOperationException(err.ToString());

            byte[] result = new byte[outLen];
            if (outLen > 0 && bodyPtr != IntPtr.Zero)
                Marshal.Copy(bodyPtr, result, 0, outLen);

            NativeTls.tls_free(bodyPtr);

            HttpResponse resp = new HttpResponse();
            resp.StatusCode = status;
            resp.Body = result;
            return resp;
        }

        public static HttpResponse Get(string url, IDictionary<string, string> headers)
        {
            return Request("GET", url, headers, null, null);
        }

        public static HttpResponse PostForm(string url, IDictionary<string, string> headers, string formBody)
        {
            byte[] body = Encoding.UTF8.GetBytes(formBody ?? "");
            return Request("POST", url, headers, "application/x-www-form-urlencoded", body);
        }

        private static bool ContainsHeader(IDictionary<string, string> headers, string name)
        {
            if (headers == null) return false;

            foreach (KeyValuePair<string, string> kv in headers)
            {
                if (kv.Key != null && string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static void Shutdown()
        {
            if (_inited)
            {
                NativeTls.tls_global_cleanup();
                _inited = false;
            }
        }
    }
}
