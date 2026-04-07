# openssl_wrp (AKA native_tls.dll)

The C wrapper exposes a small HTTPS bridge used by the ReW9x.

The wrapper is built into:

```text
build/native_tls.dll
```

## Toolchain Requirement

For old Windows targets, you need a MinGW toolchain that actually produces a
usable DLL for Win9x-era systems.

The toolchain used for this project was built from this guide:

- https://github.com/DiscordMessenger/dm/tree/master/doc/pentium-toolchain

That toolchain should be available in your `PATH`.

## OpenSSL Requirement

You also need a built OpenSSL tree with:

- headers in `include/`
- import libraries such as:
  - `libssl.dll.a`
  - `libcrypto.dll.a`

By default, the project build expects:

```text
$HOME/openssl
```

You can override this with:

```bash
OPENSSL_DIR=/path/to/openssl
```

## Manual Build

```bash
i686-w64-mingw32-gcc \
  -O2 -shared \
  -march=i386 -mtune=i386 \
  -mno-mmx -mno-sse -mno-sse2 \
  -Iexternal/openssl_wrp \
  -I"$OPENSSL_DIR/include" \
  -L"$OPENSSL_DIR" \
  -o build/native_tls.dll \
  external/openssl_wrp/openssl_wrp.c \
  external/openssl_wrp/openssl_wrp.def \
  -lssl -lcrypto -lws2_32
```