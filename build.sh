#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

mkdir -p build/providers

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
cp external/openssl_wrp/runtime/native_tls.dll build/native_tls.dll
cp external/openssl_wrp/runtime/providers/legacy.dll build/providers/legacy.dll
