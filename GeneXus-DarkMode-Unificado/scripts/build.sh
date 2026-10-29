#!/usr/bin/env bash
# Compila a GxDarkTheme.dll e o Setup-GxDarkTheme.exe fora do Windows (Linux/macOS) com o .NET SDK 8+.
# Precisa das DLLs de referencia da instalacao do GeneXus 18 numa pasta (GXREFS): Artech.*.dll, DevExpress.*v7.3.dll,
# Infragistics2.*v11.1.dll e ActiproSoftware.*.dll. Essas DLLs sao da GeneXus/terceiros e NAO vao para o git.
#   GXREFS=/caminho/das/dlls scripts/build.sh
set -euo pipefail
cd "$(dirname "$0")/.."
: "${GXREFS:?defina GXREFS com a pasta das DLLs de referencia do GeneXus 18}"
dotnet build src/GxDarkTheme.csproj -c Release -p:GeneXusDir="$GXREFS" -nologo -v q
dotnet build installer/Setup.csproj -c Release -nologo -v q
mkdir -p dist
cp bin/GxDarkTheme.dll bin/Setup-GxDarkTheme.exe dist/
sha256sum dist/* | tee dist/SHA256SUMS.txt
