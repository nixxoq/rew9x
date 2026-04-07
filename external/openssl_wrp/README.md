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

You should also set `MINGW_CC` manually to the exact compiler from that
toolchain. Do not rely on whatever `i686-w64-mingw32-gcc` happens to resolve
first on your system.

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
$MINGW_CC \
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

Example:

```bash
export MINGW_CC=i686-w64-mingw32-gcc
export OPENSSL_DIR=/home/nixxo/build/win98/src/openssl

$MINGW_CC \
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

If you do not set `MINGW_CC` manually, you may accidentally use the wrong
compiler from `PATH` and produce a `native_tls.dll` that builds but is not
stable on your target machines.

## Main Build Integration

The top-level `build.sh` already tries to build `native_tls.dll` before
compiling the main client.

That means:

1. your Win9x-compatible MinGW toolchain must already be working
2. `MINGW_CC` must be set explicitly
3. your OpenSSL build must already be available
4. then `bash build.sh` should produce:
   - `build/native_tls.dll`
   - `build/ReW9x.exe`
