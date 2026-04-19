#define _CRT_SECURE_NO_WARNINGS

#include "openssl_wrp.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

#include <winsock2.h>
#include <windows.h>

#include <openssl/ssl.h>
#include <openssl/err.h>
#include <openssl/x509v3.h>

#ifdef _MSC_VER
#pragma comment(lib, "ws2_32.lib")
#endif

#define TLS_DEFAULT_PORT "443"
#define TLS_MAX_REDIRECTS 5
#define TLS_RESPONSE_LIMIT (16 * 1024 * 1024)
#define TLS_READ_CHUNK 4096
#define TLS_REQ_BUF_SIZE 16384
#define TLS_HOST_MAX 255
#define TLS_PORT_MAX 15
#define TLS_PATH_MAX 2047
#define TLS_LOCATION_MAX 2047

typedef struct TlsConnection {
    SOCKET socket;
    SSL *ssl;
} TlsConnection;

typedef struct HttpResponseBuffer {
    unsigned char *data;
    int len;
    int header_len;
    int status_code;
    unsigned char *body;
    int body_len;
} HttpResponseBuffer;

typedef struct RedirectTarget {
    char host[TLS_HOST_MAX + 1];
    char port[TLS_PORT_MAX + 1];
    char path[TLS_PATH_MAX + 1];
} RedirectTarget;

static SSL_CTX *g_ctx = NULL;
static int g_verify_peer = 1;
static int g_wsa_started = 0;

static void set_err(char *buf, int buflen, const char *fmt, ...) {
    va_list ap;

    if (!buf || buflen <= 0)
        return;

    va_start(ap, fmt);
    vsnprintf(buf, buflen - 1, fmt, ap);
    va_end(ap);

    buf[buflen - 1] = '\0';
}

static void append_openssl_error(char *buf, int buflen, const char *prefix) {
    unsigned long e;
    char tmp[512];

    e = ERR_get_error();
    if (e) {
        ERR_error_string_n(e, tmp, sizeof(tmp));
        set_err(buf, buflen, "%s: %s", prefix, tmp);
        return;
    }

    set_err(buf, buflen, "%s", prefix);
}

static void init_connection(TlsConnection *conn) {
    conn->socket = INVALID_SOCKET;
    conn->ssl = NULL;
}

static void cleanup_connection(TlsConnection *conn) {
    if (conn->ssl) {
        SSL_free(conn->ssl);
        conn->ssl = NULL;
    }

    if (conn->socket != INVALID_SOCKET) {
        closesocket(conn->socket);
        conn->socket = INVALID_SOCKET;
    }
}

static void init_response(HttpResponseBuffer *resp) {
    resp->data = NULL;
    resp->len = 0;
    resp->header_len = 0;
    resp->status_code = 0;
    resp->body = NULL;
    resp->body_len = 0;
}

static void free_response(HttpResponseBuffer *resp) {
    if (resp->data) {
        free(resp->data);
        resp->data = NULL;
    }
}

static int is_space_or_tab(char c) {
    return c == ' ' || c == '\t';
}

static int has_ctl(const char *s) {
    const unsigned char *p;

    if (!s)
        return 1;

    p = (const unsigned char *)s;
    while (*p) {
        if (*p < 32 || *p == 127)
            return 1;
        p++;
    }

    return 0;
}

static int validate_token(const char *s, const char *name, char *errbuf, int errlen) {
    const unsigned char *p;

    if (!s || !s[0]) {
        set_err(errbuf, errlen, "%s is empty", name);
        return 0;
    }

    p = (const unsigned char *)s;
    while (*p) {
        if (*p <= 32 || *p >= 127) {
            set_err(errbuf, errlen, "%s contains invalid characters", name);
            return 0;
        }
        p++;
    }

    return 1;
}

static int validate_path(const char *path, char *errbuf, int errlen) {
    const unsigned char *p;

    if (!path || path[0] != '/') {
        set_err(errbuf, errlen, "path must start with '/'");
        return 0;
    }

    p = (const unsigned char *)path;
    while (*p) {
        if (*p == '\r' || *p == '\n') {
            set_err(errbuf, errlen, "path contains invalid characters");
            return 0;
        }
        p++;
    }

    return 1;
}

static int validate_content_type(const char *content_type, char *errbuf, int errlen) {
    const unsigned char *p;

    if (!content_type || !content_type[0])
        return 1;

    p = (const unsigned char *)content_type;
    while (*p) {
        if (*p < 32 || *p == 127 || *p == '\r' || *p == '\n') {
            set_err(errbuf, errlen, "content type contains invalid characters");
            return 0;
        }
        p++;
    }

    return 1;
}

static int parse_port(const char *port, unsigned short *out_port) {
    char *end;
    long value;

    if (!port || !port[0])
        port = TLS_DEFAULT_PORT;

    value = strtol(port, &end, 10);
    if (*end != '\0' || value <= 0 || value > 65535)
        return 0;

    *out_port = (unsigned short)value;
    return 1;
}

static int ensure_wsa(char *errbuf, int errlen) {
    WSADATA wsa;
    int rc;

    if (g_wsa_started)
        return 1;

    rc = WSAStartup(MAKEWORD(2, 2), &wsa);
    if (rc != 0) {
        set_err(errbuf, errlen, "WSAStartup failed");
        return 0;
    }

    g_wsa_started = 1;
    return 1;
}

static SOCKET connect_tcp(const char *host, const char *port, char *errbuf, int errlen) {
    struct hostent *he;
    struct sockaddr_in addr;
    SOCKET s;
    unsigned short parsed_port;

    if (!ensure_wsa(errbuf, errlen))
        return INVALID_SOCKET;

    if (!validate_token(host, "host", errbuf, errlen))
        return INVALID_SOCKET;

    if (!parse_port(port, &parsed_port)) {
        set_err(errbuf, errlen, "invalid port");
        return INVALID_SOCKET;
    }

    he = gethostbyname(host);
    if (!he || !he->h_addr_list || !he->h_addr_list[0]) {
        set_err(errbuf, errlen, "DNS failed");
        return INVALID_SOCKET;
    }

    s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET) {
        set_err(errbuf, errlen, "socket() failed");
        return INVALID_SOCKET;
    }

    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(parsed_port);
    memcpy(&addr.sin_addr, he->h_addr_list[0], sizeof(addr.sin_addr));

    if (connect(s, (struct sockaddr *)&addr, sizeof(addr)) == SOCKET_ERROR) {
        closesocket(s);
        set_err(errbuf, errlen, "connect() failed");
        return INVALID_SOCKET;
    }

    return s;
}

static int ensure_ctx(const char *ca_file, int verify_peer, char *errbuf, int errlen) {
    int requested_verify;

    requested_verify = verify_peer ? 1 : 0;
    if (g_ctx) {
        if (g_verify_peer != requested_verify) {
            set_err(errbuf, errlen, "TLS already initialized with a different verification mode");
            return 0;
        }
        return 1;
    }

    OPENSSL_init_ssl(0, NULL);

    g_ctx = SSL_CTX_new(TLS_client_method());
    if (!g_ctx) {
        append_openssl_error(errbuf, errlen, "SSL_CTX_new failed");
        return 0;
    }

    g_verify_peer = requested_verify;
    SSL_CTX_set_verify(g_ctx, g_verify_peer ? SSL_VERIFY_PEER : SSL_VERIFY_NONE, NULL);

    if (ca_file && ca_file[0]) {
        if (SSL_CTX_load_verify_locations(g_ctx, ca_file, NULL) != 1) {
            append_openssl_error(errbuf, errlen, "Loading CA failed");
            SSL_CTX_free(g_ctx);
            g_ctx = NULL;
            return 0;
        }
    } else if (g_verify_peer) {
        set_err(errbuf, errlen, "CA file is required when peer verification is enabled");
        SSL_CTX_free(g_ctx);
        g_ctx = NULL;
        return 0;
    }

#if defined(TLS1_2_VERSION)
    SSL_CTX_set_min_proto_version(g_ctx, TLS1_2_VERSION);
#endif

    return 1;
}

static int configure_hostname_verification(SSL *ssl, const char *host, char *errbuf, int errlen) {
    if (!g_verify_peer)
        return 1;

#if OPENSSL_VERSION_NUMBER >= 0x10100000L // >=1.1.0
    X509_VERIFY_PARAM *param;

    param = SSL_get0_param(ssl);
    if (!param) {
        set_err(errbuf, errlen, "TLS verify parameters unavailable");
        return 0;
    }

    X509_VERIFY_PARAM_set_hostflags(param, X509_CHECK_FLAG_NO_PARTIAL_WILDCARDS);
    if (X509_VERIFY_PARAM_set1_host(param, host, 0) != 1) {
        append_openssl_error(errbuf, errlen, "TLS hostname setup failed");
        return 0;
    }
#else
    (void)ssl;
    (void)host;
    set_err(errbuf, errlen, "Hostname verification requires OpenSSL 1.1.0 or newer");
    return 0;
#endif

    return 1;
}

static int open_tls_connection(const char *host, const char *port, TlsConnection *conn, char *errbuf, int errlen) {
    long verify_result;

    init_connection(conn);

    conn->socket = connect_tcp(host, port, errbuf, errlen);
    if (conn->socket == INVALID_SOCKET)
        return 0;

    conn->ssl = SSL_new(g_ctx);
    if (!conn->ssl) {
        append_openssl_error(errbuf, errlen, "SSL_new failed");
        cleanup_connection(conn);
        return 0;
    }

    if (SSL_set_fd(conn->ssl, (int)conn->socket) != 1) {
        append_openssl_error(errbuf, errlen, "SSL_set_fd failed");
        cleanup_connection(conn);
        return 0;
    }

    if (SSL_set_tlsext_host_name(conn->ssl, host) != 1) {
        append_openssl_error(errbuf, errlen, "TLS SNI setup failed");
        cleanup_connection(conn);
        return 0;
    }

    if (!configure_hostname_verification(conn->ssl, host, errbuf, errlen)) {
        cleanup_connection(conn);
        return 0;
    }

    if (SSL_connect(conn->ssl) != 1) {
        append_openssl_error(errbuf, errlen, "TLS handshake failed");
        cleanup_connection(conn);
        return 0;
    }

    if (g_verify_peer) {
        verify_result = SSL_get_verify_result(conn->ssl);
        if (verify_result != X509_V_OK) {
            set_err(errbuf, errlen, "TLS certificate verification failed: %s",
                    X509_verify_cert_error_string(verify_result));
            cleanup_connection(conn);
            return 0;
        }
    }

    return 1;
}

static int write_all_ssl(SSL *ssl, const unsigned char *buf, int len, char *errbuf, int errlen) {
    int off;

    off = 0;
    while (off < len) {
        int rc;

        rc = SSL_write(ssl, buf + off, len - off);
        if (rc <= 0) {
            append_openssl_error(errbuf, errlen, "SSL_write failed");
            return 0;
        }

        off += rc;
    }

    return 1;
}

static int header_exists(const char *headers, const char *name) {
    int name_len;
    const char *p;

    if (!headers || !name)
        return 0;

    name_len = (int)strlen(name);
    p = headers;
    while (*p) {
        const char *line_start;

        line_start = p;
        if (_strnicmp(line_start, name, name_len) == 0 && line_start[name_len] == ':')
            return 1;

        p = strstr(line_start, "\r\n");
        if (!p)
            break;
        p += 2;
    }

    return 0;
}

static int build_host_header(const char *host, const char *port, char *out, int outlen) {
    int n;

    if (!port || !port[0] || strcmp(port, TLS_DEFAULT_PORT) == 0) {
        n = _snprintf(out, outlen, "%s", host);
    } else {
        n = _snprintf(out, outlen, "%s:%s", host, port);
    }

    if (n < 0 || n >= outlen)
        return 0;

    return 1;
}

static int send_http_request(
    SSL *ssl,
    const char *method,
    const char *host,
    const char *port,
    const char *path,
    const char *content_type,
    const unsigned char *body,
    int body_len,
    const char *extra_headers,
    char *errbuf,
    int errlen
) {
    char req[TLS_REQ_BUF_SIZE];
    char host_header[TLS_HOST_MAX + TLS_PORT_MAX + 3];
    char content_type_header[512];
    char content_length_header[64];
    int req_len;
    int has_entity;

    content_type_header[0] = '\0';
    content_length_header[0] = '\0';

    if (body_len < 0) {
        set_err(errbuf, errlen, "body length is negative");
        return 0;
    }

    if (body_len > 0 && !body) {
        set_err(errbuf, errlen, "body pointer is null");
        return 0;
    }

    if (!validate_content_type(content_type, errbuf, errlen))
        return 0;

    if (!build_host_header(host, port, host_header, sizeof(host_header))) {
        set_err(errbuf, errlen, "Host header is too long");
        return 0;
    }

    has_entity = body_len > 0 || (content_type && content_type[0]);
    if (content_type && content_type[0] && !header_exists(extra_headers, "Content-Type")) {
        req_len = _snprintf(content_type_header, sizeof(content_type_header),
                            "Content-Type: %s\r\n", content_type);
        if (req_len < 0 || req_len >= (int)sizeof(content_type_header)) {
            set_err(errbuf, errlen, "Content-Type header is too long");
            return 0;
        }
    }

    if (has_entity && !header_exists(extra_headers, "Content-Length")) {
        req_len = _snprintf(content_length_header, sizeof(content_length_header),
                            "Content-Length: %d\r\n", body_len);
        if (req_len < 0 || req_len >= (int)sizeof(content_length_header)) {
            set_err(errbuf, errlen, "Content-Length header failed");
            return 0;
        }
    }

    req_len = _snprintf(req, sizeof(req),
                        "%s %s HTTP/1.1\r\n"
                        "Host: %s\r\n"
                        "%s"
                        "%s"
                        "%s"
                        "Connection: close\r\n"
                        "\r\n",
                        method,
                        path,
                        host_header,
                        extra_headers ? extra_headers : "",
                        content_type_header,
                        content_length_header);

    if (req_len < 0 || req_len >= (int)sizeof(req)) {
        set_err(errbuf, errlen, "HTTP request headers are too large");
        return 0;
    }

    if (!write_all_ssl(ssl, (const unsigned char *)req, req_len, errbuf, errlen))
        return 0;

    if (body_len > 0) {
        if (!write_all_ssl(ssl, body, body_len, errbuf, errlen))
            return 0;
    }

    return 1;
}

static int read_all_ssl(SSL *ssl, HttpResponseBuffer *resp, char *errbuf, int errlen) {
    int cap;

    cap = 0;
    for (;;) {
        unsigned char tmp[TLS_READ_CHUNK];
        int rc;

        rc = SSL_read(ssl, tmp, sizeof(tmp));
        if (rc > 0) {
            if (resp->len > TLS_RESPONSE_LIMIT - rc) {
                free_response(resp);
                set_err(errbuf, errlen, "HTTP response exceeded %d bytes", TLS_RESPONSE_LIMIT);
                return 0;
            }

            if (resp->len + rc + 1 > cap) {
                int needed;
                int newcap;
                unsigned char *newdata;

                needed = resp->len + rc + 1;
                newcap = cap ? cap * 2 : 8192;
                while (newcap < needed)
                    newcap *= 2;

                if (newcap > TLS_RESPONSE_LIMIT + 1)
                    newcap = TLS_RESPONSE_LIMIT + 1;

                newdata = (unsigned char *)realloc(resp->data, newcap);
                if (!newdata) {
                    free_response(resp);
                    set_err(errbuf, errlen, "Out of memory while reading response");
                    return 0;
                }

                resp->data = newdata;
                cap = newcap;
            }

            memcpy(resp->data + resp->len, tmp, rc);
            resp->len += rc;
        } else {
            int ssl_error;

            ssl_error = SSL_get_error(ssl, rc);
            if (ssl_error == SSL_ERROR_ZERO_RETURN)
                break;

            if (ssl_error == SSL_ERROR_SYSCALL && resp->len > 0)
                break;

            free_response(resp);
            append_openssl_error(errbuf, errlen, "SSL_read failed");
            return 0;
        }
    }

    if (!resp->data) {
        resp->data = (unsigned char *)malloc(1);
        if (!resp->data) {
            set_err(errbuf, errlen, "Out of memory while reading response");
            return 0;
        }
    }

    resp->data[resp->len] = '\0';
    return 1;
}

static char *find_header_end(unsigned char *buf, int len) {
    int i;

    for (i = 0; i + 3 < len; i++) {
        if (buf[i] == '\r' &&
            buf[i + 1] == '\n' &&
            buf[i + 2] == '\r' &&
            buf[i + 3] == '\n') {
            return (char *)(buf + i + 4);
        }
    }

    return NULL;
}

static int parse_status_code(const unsigned char *buf, int len) {
    int code;
    int i;

    code = 0;
    i = 0;

    while (i < len && buf[i] != ' ')
        i++;

    while (i < len && buf[i] == ' ')
        i++;

    while (i < len && buf[i] >= '0' && buf[i] <= '9') {
        code = code * 10 + (buf[i] - '0');
        i++;
    }

    return code;
}

static int split_response(HttpResponseBuffer *resp, char *errbuf, int errlen) {
    char *body_ptr;

    body_ptr = find_header_end(resp->data, resp->len);
    if (!body_ptr) {
        set_err(errbuf, errlen, "HTTP response missing header terminator");
        return 0;
    }

    resp->header_len = (int)(body_ptr - (char *)resp->data);
    resp->status_code = parse_status_code(resp->data, resp->header_len);
    if (resp->status_code < 100 || resp->status_code > 999) {
        set_err(errbuf, errlen, "HTTP response has invalid status");
        return 0;
    }

    resp->body = (unsigned char *)body_ptr;
    resp->body_len = resp->len - resp->header_len;
    return 1;
}

static int extract_header_value(
    const unsigned char *hdr,
    int hdrlen,
    const char *name,
    char *out,
    int outlen
) {
    int name_len;
    int i;

    if (!out || outlen <= 0)
        return 0;

    out[0] = '\0';
    name_len = (int)strlen(name);
    i = 0;

    while (i < hdrlen) {
        int line_start;
        int line_end;
        int value_start;
        int value_len;

        line_start = i;
        while (i < hdrlen && hdr[i] != '\r' && hdr[i] != '\n')
            i++;
        line_end = i;

        if (line_end - line_start > name_len &&
            _strnicmp((const char *)hdr + line_start, name, name_len) == 0 &&
            hdr[line_start + name_len] == ':') {
            value_start = line_start + name_len + 1;
            while (value_start < line_end && is_space_or_tab((char)hdr[value_start]))
                value_start++;

            value_len = line_end - value_start;
            if (value_len >= outlen)
                value_len = outlen - 1;

            memcpy(out, hdr + value_start, value_len);
            out[value_len] = '\0';
            return 1;
        }

        while (i < hdrlen && (hdr[i] == '\r' || hdr[i] == '\n'))
            i++;
    }

    return 0;
}

static int is_redirect_status(int status) {
    return status == 301 ||
           status == 302 ||
           status == 303 ||
           status == 307 ||
           status == 308;
}

static int copy_segment(char *dst, int dstlen, const char *src, int srclen) {
    if (srclen <= 0 || srclen >= dstlen)
        return 0;

    memcpy(dst, src, srclen);
    dst[srclen] = '\0';
    return 1;
}

static int parse_https_redirect(const char *location, RedirectTarget *target, char *errbuf, int errlen) {
    const char *prefix;
    const char *host_start;
    const char *host_end;
    const char *slash;
    const char *colon;
    int host_len;
    int port_len;
    int path_len;

    prefix = "https://";
    if (!location || _strnicmp(location, prefix, 8) != 0) {
        set_err(errbuf, errlen, "Redirect target is not https://");
        return 0;
    }

    host_start = location + 8;
    slash = strchr(host_start, '/');
    if (!slash) {
        set_err(errbuf, errlen, "Redirect target has no path");
        return 0;
    }

    host_end = slash;
    colon = NULL;
    {
        const char *p;

        for (p = host_start; p < host_end; p++) {
            if (*p == ':') {
                colon = p;
                break;
            }
        }
    }

    if (colon) {
        unsigned short parsed_port;

        host_len = (int)(colon - host_start);
        port_len = (int)(host_end - colon - 1);
        if (!copy_segment(target->port, sizeof(target->port), colon + 1, port_len) ||
            !parse_port(target->port, &parsed_port)) {
            set_err(errbuf, errlen, "Redirect target has invalid port");
            return 0;
        }
    } else {
        host_len = (int)(host_end - host_start);
        strcpy(target->port, TLS_DEFAULT_PORT);
    }

    path_len = (int)strlen(slash);
    if (!copy_segment(target->host, sizeof(target->host), host_start, host_len) ||
        !copy_segment(target->path, sizeof(target->path), slash, path_len)) {
        set_err(errbuf, errlen, "Redirect target is too long");
        return 0;
    }

    if (has_ctl(target->host) || has_ctl(target->path)) {
        set_err(errbuf, errlen, "Redirect target contains invalid characters");
        return 0;
    }

    return 1;
}

static int validate_request_args(
    const char *method,
    const char *host,
    const char *path,
    unsigned char **out_body,
    int *out_body_len,
    char *errbuf,
    int errlen
) {
    if (!g_ctx) {
        set_err(errbuf, errlen, "Call tls_global_init first");
        return 0;
    }

    if (!out_body || !out_body_len) {
        set_err(errbuf, errlen, "output pointers are required");
        return 0;
    }

    *out_body = NULL;
    *out_body_len = 0;

    if (!validate_token(method, "method", errbuf, errlen))
        return 0;

    if (!validate_token(host, "host", errbuf, errlen))
        return 0;

    if (!validate_path(path, errbuf, errlen))
        return 0;

    return 1;
}

TLS_API int TLS_CALL https_request(
    const char *method,
    const char *host,
    const char *port,
    const char *path,
    const char *content_type,
    const unsigned char *body,
    int body_len,
    const char *extra_headers,
    unsigned char **out_body,
    int *out_body_len,
    int *out_status_code,
    char *errbuf,
    int errlen
) {
    const char *current_host;
    const char *current_port;
    const char *current_path;
    RedirectTarget redirect;
    int redirects;

    if (!validate_request_args(method, host, path, out_body, out_body_len, errbuf, errlen))
        return 0;

    if (out_status_code)
        *out_status_code = 0;

    current_host = host;
    current_port = port && port[0] ? port : TLS_DEFAULT_PORT;
    current_path = path;
    redirects = 0;

    for (;;) {
        TlsConnection conn;
        HttpResponseBuffer resp;
        unsigned char *final_body;

        init_connection(&conn);
        init_response(&resp);
        final_body = NULL;

        if (!open_tls_connection(current_host, current_port, &conn, errbuf, errlen))
            return 0;

        if (!send_http_request(
                conn.ssl,
                method,
                current_host,
                current_port,
                current_path,
                content_type,
                body,
                body_len,
                extra_headers,
                errbuf,
                errlen)) {
            cleanup_connection(&conn);
            return 0;
        }

        if (!read_all_ssl(conn.ssl, &resp, errbuf, errlen)) {
            cleanup_connection(&conn);
            return 0;
        }

        if (!split_response(&resp, errbuf, errlen)) {
            free_response(&resp);
            cleanup_connection(&conn);
            return 0;
        }

        if (is_redirect_status(resp.status_code)) {
            char location[TLS_LOCATION_MAX + 1];

            if (!extract_header_value(resp.data, resp.header_len, "Location", location, sizeof(location))) {
                set_err(errbuf, errlen, "Redirect response missing Location header");
                free_response(&resp);
                cleanup_connection(&conn);
                return 0;
            }

            if (redirects >= TLS_MAX_REDIRECTS) {
                set_err(errbuf, errlen, "Too many redirects");
                free_response(&resp);
                cleanup_connection(&conn);
                return 0;
            }

            if (!parse_https_redirect(location, &redirect, errbuf, errlen)) {
                free_response(&resp);
                cleanup_connection(&conn);
                return 0;
            }

            redirects++;
            current_host = redirect.host;
            current_port = redirect.port;
            current_path = redirect.path;
            free_response(&resp);
            cleanup_connection(&conn);
            continue;
        }

        final_body = (unsigned char *)malloc(resp.body_len + 1);
        if (!final_body) {
            set_err(errbuf, errlen, "Out of memory while copying response body");
            free_response(&resp);
            cleanup_connection(&conn);
            return 0;
        }

        if (resp.body_len > 0)
            memcpy(final_body, resp.body, resp.body_len);
        final_body[resp.body_len] = '\0';

        *out_body = final_body;
        *out_body_len = resp.body_len;
        if (out_status_code)
            *out_status_code = resp.status_code;

        free_response(&resp);
        cleanup_connection(&conn);
        return 1;
    }
}

TLS_API int TLS_CALL tls_global_init(const char *ca_file, int verify_peer, char *errbuf, int errlen) {
    if (!ensure_ctx(ca_file, verify_peer, errbuf, errlen))
        return 0;

    if (!ensure_wsa(errbuf, errlen))
        return 0;

    return 1;
}

TLS_API void TLS_CALL tls_global_cleanup(void) {
    if (g_ctx) {
        SSL_CTX_free(g_ctx);
        g_ctx = NULL;
    }

    if (g_wsa_started) {
        WSACleanup();
        g_wsa_started = 0;
    }
}

TLS_API void TLS_CALL tls_free(void *p) {
    if (p)
        free(p);
}
