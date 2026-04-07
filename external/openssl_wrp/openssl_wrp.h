#ifndef NATIVE_TLS_H
#define NATIVE_TLS_H

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#define TLS_API __declspec(dllexport)
#define TLS_CALL __cdecl
#else
#define TLS_API
#define TLS_CALL
#endif

TLS_API int TLS_CALL tls_global_init(const char *ca_file, int verify_peer, char *errbuf, int errlen);
TLS_API void TLS_CALL tls_global_cleanup(void);
TLS_API void TLS_CALL tls_free(void *p);
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
);

#ifdef __cplusplus
}
#endif

#endif
