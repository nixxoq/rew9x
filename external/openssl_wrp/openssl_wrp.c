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

#pragma comment(lib, "ws2_32.lib")

static SSL_CTX *g_ctx = NULL;
static int g_verify_peer = 1;
static int g_wsa_started = 0;

static void set_err(char *buf, int buflen, const char *fmt, ...) {
    if (!buf || buflen <= 0) return;

    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, buflen - 1, fmt, ap);
    va_end(ap);

    buf[buflen - 1] = '\0';
}

static void append_openssl_error(
    char *buf,
    int buflen,
    const char *prefix
) {
    unsigned long e =
    ERR_get_error();

    char tmp[512];

    if (e) {

        ERR_error_string_n(
            e,
            tmp,
            sizeof(tmp));

        set_err(
            buf,
            buflen,
            "%s: %s",
            prefix,
            tmp);

    } else {

        set_err(
            buf,
            buflen,
            "%s",
            prefix);
    }
}

static int ensure_wsa(
    char *errbuf,
    int errlen
) {
    if (g_wsa_started)
        return 1;

    WSADATA wsa;

    int rc =
    WSAStartup(
        MAKEWORD(2,2),
               &wsa);

    if (rc != 0) {

        set_err(
            errbuf,
            errlen,
            "WSAStartup failed");

        return 0;
    }

    g_wsa_started = 1;

    return 1;
}

static SOCKET connect_tcp(
    const char *host,
    const char *port,
    char *errbuf,
    int errlen
) {
    struct hostent *he;

    struct sockaddr_in addr;

    SOCKET s;

    if (!ensure_wsa(
        errbuf,
        errlen))
        return INVALID_SOCKET;

    he =
    gethostbyname(host);

    if (!he) {

        set_err(
            errbuf,
            errlen,
            "DNS failed");

        return INVALID_SOCKET;
    }

    s =
    socket(
        AF_INET,
        SOCK_STREAM,
        IPPROTO_TCP);

    if (s == INVALID_SOCKET) {

        set_err(
            errbuf,
            errlen,
            "socket() failed");

        return INVALID_SOCKET;
    }

    memset(
        &addr,
        0,
        sizeof(addr));

    addr.sin_family = AF_INET;

    addr.sin_port =
    htons(
        atoi(
            port ? port : "443"));

    memcpy(
        &addr.sin_addr,
        he->h_addr_list[0],
        sizeof(addr.sin_addr));

    if (connect(
        s,
        (struct sockaddr*)&addr,
                sizeof(addr)) == SOCKET_ERROR) {

        closesocket(s);

    set_err(
        errbuf,
        errlen,
        "connect() failed");

    return INVALID_SOCKET;
                }

                return s;
}

static int ensure_ctx(
    const char *ca_file,
    int verify_peer,
    char *errbuf,
    int errlen
) {
    if (g_ctx)
        return 1;

    OPENSSL_init_ssl(0,NULL);

    g_ctx =
    SSL_CTX_new(
        TLS_client_method());

    if (!g_ctx) {

        append_openssl_error(
            errbuf,
            errlen,
            "SSL_CTX_new failed");

        return 0;
    }

    g_verify_peer =
    verify_peer ? 1 : 0;

    SSL_CTX_set_verify(
        g_ctx,
        g_verify_peer ?
        SSL_VERIFY_PEER :
        SSL_VERIFY_NONE,
        NULL);

    if (ca_file &&
        ca_file[0]) {

        if (SSL_CTX_load_verify_locations(
            g_ctx,
            ca_file,
            NULL) != 1) {

            append_openssl_error(
                errbuf,
                errlen,
                "Loading CA failed");

            return 0;
            }
        }

        #if defined(SSL_CTX_set_min_proto_version)

        SSL_CTX_set_min_proto_version(
            g_ctx,
            TLS1_2_VERSION);

        #endif

        return 1;
}

static int write_all_ssl(
    SSL *ssl,
    const unsigned char *buf,
    int len
) {
    int off = 0;

    while (off < len) {

        int rc =
        SSL_write(
            ssl,
            buf + off,
            len - off);

        if (rc <= 0)
            return 0;

        off += rc;
    }

    return 1;
}

static int read_all_ssl(
    SSL *ssl,
    unsigned char **out,
    int *out_len,
    char *errbuf,
    int errlen
) {
    unsigned char *data = NULL;

    int size = 0;

    int cap = 0;

    for (;;) {

        unsigned char tmp[4096];

        int rc =
        SSL_read(
            ssl,
            tmp,
            sizeof(tmp));

        if (rc > 0) {

            if (size + rc + 1 > cap) {

                int newcap =
                cap ? cap * 2 : 8192;

                while (newcap <
                    size + rc + 1)
                    newcap *= 2;

                data =
                realloc(
                    data,
                    newcap);

                cap = newcap;
            }

            memcpy(
                data + size,
                tmp,
                rc);

            size += rc;

        } else {

            int e =
            SSL_get_error(
                ssl,
                rc);

            if (e ==
                SSL_ERROR_ZERO_RETURN)
                break;

            if (e ==
                SSL_ERROR_SYSCALL &&
                size > 0)
                break;

            free(data);

            append_openssl_error(
                errbuf,
                errlen,
                "SSL_read failed");

            return 0;
        }
    }

    if (!data)
        data = malloc(1);

    data[size] = 0;

    *out = data;
    *out_len = size;

    return 1;
}

static char *find_header_end(
    unsigned char *buf,
    int len
) {
    int i;

    for (i=0;i+3<len;i++) {

        if (buf[i]=='\r' &&
            buf[i+1]=='\n' &&
            buf[i+2]=='\r' &&
            buf[i+3]=='\n')

            return (char*)
            (buf+i+4);
    }

    return NULL;
}

static int parse_status_code(
    const unsigned char *buf,
    int len
) {
    int code = 0;

    int i=0;

    while (i<len &&
        buf[i]!=' ')
        i++;

    while (i<len &&
        buf[i]==' ')
        i++;

    while (i<len &&
        buf[i]>='0' &&
        buf[i]<='9') {

        code =
        code*10 +
        (buf[i]-'0');

    i++;
        }

        return code;
}

static int extract_location(
    const unsigned char *hdr,
    int hdrlen,
    char *out,
    int outlen
) {
    const char *needle =
    "Location:";

    int nlen =
    strlen(needle);

    int i;

    for (i=0;
         i+nlen<hdrlen;
    i++) {

        if (_strnicmp(
            (char*)hdr+i,
                      needle,
                      nlen)==0) {

            const char *p =
            (char*)hdr+i+nlen;

        while (*p==' '||
            *p=='\t')
            p++;

        int j=0;

        while (*p &&
            *p!='\r' &&
            *p!='\n' &&
            j<outlen-1) {

            out[j++]=*p++;
            }

            out[j]=0;

        return 1;
                      }
    }

    return 0;
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

    int redirects = 0;

    retry_request:

    SOCKET s =
    connect_tcp(
        host,
        port,
        errbuf,
        errlen);

    if (s ==
        INVALID_SOCKET)
        return 0;

    SSL *ssl =
    SSL_new(g_ctx);

    SSL_set_fd(
        ssl,
        (int)s);

    SSL_set_tlsext_host_name(
        ssl,
        host);

    if (SSL_connect(
        ssl)!=1) {

        append_openssl_error(
            errbuf,
            errlen,
            "TLS handshake failed");

        return 0;
        }

        char req[16384];

        int req_len =
        _snprintf(
            req,
            sizeof(req)-1,

                  "%s %s HTTP/1.1\r\n"
                  "Host: %s\r\n"
                  "%s"
                  "Connection: close\r\n"
                  "\r\n",

                  method,
                  path,
                  host,

                  extra_headers ?
                  extra_headers : ""
        );

        if (!write_all_ssl(
            ssl,
            (unsigned char*)req,
                           req_len))
            return 0;

        unsigned char *raw;

        int rawlen;

        if (!read_all_ssl(
            ssl,
            &raw,
            &rawlen,
            errbuf,
            errlen))
            return 0;

        char *body_ptr =
        find_header_end(
            raw,
            rawlen);

        int hdrlen =
        body_ptr -
        (char*)raw;

        int status =
        parse_status_code(
            raw,
            hdrlen);

        if (status==301 ||
            status==302) {

            char location[2048];

        if (extract_location(
            raw,
            hdrlen,
            location,
            sizeof(location))) {

            if (redirects<5) {

                redirects++;

                const char *p =
                location+8;

                const char *slash =
                strchr(p,'/');

                static char new_host[256];
                static char new_path[1024];

                strncpy(
                    new_host,
                    p,
                    slash-p);

                new_host[slash-p]=0;

                strcpy(
                    new_path,
                    slash);

                host=new_host;
                path=new_path;

                free(raw);

                SSL_free(ssl);

                closesocket(s);

                goto retry_request;
            }
            }
            }

            int bodylen =
            rawlen - hdrlen;

            unsigned char *final =
            malloc(bodylen+1);

            memcpy(
                final,
                body_ptr,
                bodylen);

            final[bodylen]=0;

            *out_body = final;
            *out_body_len = bodylen;

            if (out_status_code)
                *out_status_code = status;

    free(raw);

    SSL_free(ssl);

    closesocket(s);

    return 1;
}

TLS_API int TLS_CALL tls_global_init(
    const char *ca_file,
    int verify_peer,
    char *errbuf,
    int errlen
) {
    if (!ensure_ctx(
        ca_file,
        verify_peer,
        errbuf,
        errlen))
        return 0;

    if (!ensure_wsa(
        errbuf,
        errlen))
        return 0;

    return 1;
}

TLS_API void TLS_CALL tls_global_cleanup(
    void
) {
    if (g_ctx) {

        SSL_CTX_free(
            g_ctx);

        g_ctx=NULL;
    }

    if (g_wsa_started) {

        WSACleanup();

        g_wsa_started=0;
    }
}

TLS_API void TLS_CALL tls_free(
    void *p
) {
    if (p)
        free(p);
}
