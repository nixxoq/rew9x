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

    internal struct BomEncoding
    {
        public readonly byte[] Prefix;
        public readonly Encoding Encoding;

        public BomEncoding(byte[] prefix, Encoding encoding)
        {
            Prefix = prefix;
            Encoding = encoding;
        }
    }

    public static class EncodingDetector
    {
        private static readonly BomEncoding[] BomEncodings =
            new BomEncoding[]
            {
                new BomEncoding(new byte[] { 0xEF, 0xBB, 0xBF }, Encoding.UTF8),
                new BomEncoding(new byte[] { 0xFF, 0xFE }, Encoding.Unicode),
                new BomEncoding(new byte[] { 0xFE, 0xFF }, Encoding.BigEndianUnicode)
            };

        public static string Decode(byte[] body)
        {
            Encoding enc = Detect(body);
            return enc.GetString(body);
        }

        public static Encoding Detect(byte[] body)
        {
            Encoding bomEncoding;
            Encoding charsetEncoding;

            if (body == null || body.Length == 0)
                return Encoding.UTF8;

            bomEncoding = DetectBom(body);
            if (bomEncoding != null)
                return bomEncoding;

            charsetEncoding = DetectCharset(body);
            if (charsetEncoding != null)
                return charsetEncoding;

            return Encoding.UTF8;
        }

        private static Encoding DetectBom(byte[] body)
        {
            int i;

            for (i = 0; i < BomEncodings.Length; i++)
            {
                if (StartsWith(body, BomEncodings[i].Prefix))
                    return BomEncodings[i].Encoding;
            }

            return null;
        }

        private static bool StartsWith(byte[] body, byte[] prefix)
        {
            int i;

            if (body.Length < prefix.Length)
                return false;

            for (i = 0; i < prefix.Length; i++)
            {
                if (body[i] != prefix[i])
                    return false;
            }

            return true;
        }

        private static Encoding DetectCharset(byte[] body)
        {
            int len = body.Length < 4096 ? body.Length : 4096;
            string head = Encoding.ASCII.GetString(body, 0, len);
            int idx = head.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);

            if (idx < 0)
                return null;

            idx += 8;
            while (idx < head.Length && IsCharsetPadding(head[idx]))
                idx++;

            int end = idx;
            while (end < head.Length && !IsCharsetTerminator(head[end]))
                end++;

            string name = head.Substring(idx, end - idx).Trim();
            name = name.Trim('"', '\'', ' ', ';');

            if (name.Length == 0)
                return null;

            try
            {
                return Encoding.GetEncoding(name);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCharsetPadding(char c)
        {
            return c == '"' || c == '\'' || c == ' ';
        }

        private static bool IsCharsetTerminator(char c)
        {
            return c == ';' ||
                   c == '"' ||
                   c == '\'' ||
                   c == '>' ||
                   c == '\r' ||
                   c == '\n' ||
                   c == ' ';
        }
    }

    internal struct HeaderSpec
    {
        public readonly string Name;
        public readonly string Value;

        public HeaderSpec(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    internal static class HeaderBuilder
    {
        private static readonly HeaderSpec[] DefaultHeaders =
            new HeaderSpec[]
            {
                new HeaderSpec("User-Agent", ReW9x.AppConfig.UserAgent),
                new HeaderSpec("Accept", "*/*"),
                new HeaderSpec("Accept-Encoding", "identity")
            };

        public static string Build(IDictionary<string, string> headers)
        {
            StringBuilder extra = new StringBuilder();
            int i;

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> kv in headers)
                {
                    AppendUserHeader(extra, kv.Key, kv.Value);
                }
            }

            for (i = 0; i < DefaultHeaders.Length; i++)
            {
                if (!ContainsHeader(headers, DefaultHeaders[i].Name))
                    AppendHeader(extra, DefaultHeaders[i].Name, DefaultHeaders[i].Value);
            }

            return extra.ToString();
        }

        private static void AppendUserHeader(StringBuilder extra, string name, string value)
        {
            ValidateHeader(name, value);

            if (IsNativeOwnedHeader(name))
                return;

            AppendHeader(extra, name, value);
        }

        private static void AppendHeader(StringBuilder extra, string name, string value)
        {
            extra.Append(name);
            extra.Append(": ");
            extra.Append(value == null ? "" : value);
            extra.Append("\r\n");
        }

        private static void ValidateHeader(string name, string value)
        {
            int i;

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Header name is empty.", "headers");

            for (i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c <= 32 || c >= 127 || c == ':')
                    throw new ArgumentException("Header name contains invalid characters.", "headers");
            }

            if (value == null)
                return;

            for (i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\r' || c == '\n')
                    throw new ArgumentException("Header value contains invalid characters.", "headers");
            }
        }

        private static bool IsNativeOwnedHeader(string name)
        {
            return HeaderNameEquals(name, "Host") ||
                   HeaderNameEquals(name, "Connection") ||
                   HeaderNameEquals(name, "Content-Length");
        }

        private static bool ContainsHeader(IDictionary<string, string> headers, string name)
        {
            if (headers == null)
                return false;

            foreach (KeyValuePair<string, string> kv in headers)
            {
                if (HeaderNameEquals(kv.Key, name))
                    return true;
            }

            return false;
        }

        private static bool HeaderNameEquals(string left, string right)
        {
            return left != null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
            string extraHeaders = HeaderBuilder.Build(headers);

            StringBuilder err = new StringBuilder(1024);
            IntPtr bodyPtr = IntPtr.Zero;
            int outLen = 0;
            int status = 0;

            try
            {
                int rc = NativeTls.https_request(
                    method,
                    host,
                    port,
                    path,
                    contentType,
                    body,
                    body == null ? 0 : body.Length,
                    extraHeaders,
                    out bodyPtr,
                    out outLen,
                    out status,
                    err,
                    err.Capacity
                );

                if (rc == 0)
                    throw new InvalidOperationException(err.ToString());

                if (outLen < 0)
                    throw new InvalidOperationException("Native TLS returned an invalid body length.");

                byte[] result = new byte[outLen];
                if (outLen > 0 && bodyPtr != IntPtr.Zero)
                    Marshal.Copy(bodyPtr, result, 0, outLen);

                HttpResponse resp = new HttpResponse();
                resp.StatusCode = status;
                resp.Body = result;
                return resp;
            }
            finally
            {
                NativeTls.tls_free(bodyPtr);
            }
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
