#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

OPENSSL_DIR="${OPENSSL_DIR:-$HOME/openssl}"
MINGW_CC="${MINGW_CC:-i686-w64-mingw32-gcc}"

mkdir -p build/providers

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
