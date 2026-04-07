#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mcs -sdk:2 -platform:x86 -target:winexe -out:Reddit98Client.exe \
  Program.cs AppConfig.cs \
  Models/*.cs Parsing/*.cs Storage/*.cs Native/*.cs Net/*.cs UI/*.cs \
  -r:System.Windows.Forms.dll \
  -r:System.Drawing.dll
