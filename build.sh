#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

OPENSSL_DIR="${OPENSSL_DIR:-$HOME/openssl}"

mkdir -p build/providers

if [ -z "${MINGW_CC:-}" ]; then
  echo "MINGW_CC is not set." >&2
  echo "Set it explicitly to the exact compiler from your Win9x-compatible toolchain." >&2
  echo "Example:" >&2
  echo "export PATH=/path/to/custom/mingw/build/cross/bin:$PATH" >&2
  echo "MINGW_CC=i686-w64-mingw32-gcc OPENSSL_DIR=/path/to/openssl bash build.sh" >&2
  exit 1
fi

if [ ! -d "$OPENSSL_DIR/include" ]; then
  echo "OpenSSL include directory not found: $OPENSSL_DIR/include" >&2
  exit 1
fi

if [ ! -f "$OPENSSL_DIR/libssl.dll.a" ] || [ ! -f "$OPENSSL_DIR/libcrypto.dll.a" ]; then
  echo "OpenSSL import libraries not found in: $OPENSSL_DIR" >&2
  exit 1
fi

"$MINGW_CC" \
  -shared \
  -O2 \
  -march=i386 \
  -mtune=i386 \
  -mno-mmx \
  -mno-sse \
  -mno-sse2 \
  -Iexternal/openssl_wrp \
  -I"$OPENSSL_DIR/include" \
  -L"$OPENSSL_DIR" \
  -o build/native_tls.dll \
  external/openssl_wrp/openssl_wrp.c \
  external/openssl_wrp/openssl_wrp.def \
  -lssl \
  -lcrypto \
  -lws2_32

mcs -sdk:2 -platform:x86 -target:winexe -out:build/ReW9x.exe \
  src/app/*.cs \
  src/models/*.cs \
  src/utils/*.cs \
  external/openssl_wrp/*.cs \
  src/api/*.cs \
  src/ui/*.cs \
  src/ui/main/*.cs \
  -r:System.Windows.Forms.dll \
  -r:System.Drawing.dll

cp external/openssl_wrp/runtime/cacert.pem build/cacert.pem
cp external/openssl_wrp/runtime/libcrypto-3.dll build/libcrypto-3.dll
cp external/openssl_wrp/runtime/libssl-3.dll build/libssl-3.dll
cp external/openssl_wrp/runtime/providers/legacy.dll build/providers/legacy.dll
