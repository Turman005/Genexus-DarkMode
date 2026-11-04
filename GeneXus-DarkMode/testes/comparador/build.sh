#!/bin/bash
# Teste de replica do comparador (box). Uso: ./build.sh [pasta-das-previas]
set -e
cd "$(dirname "$0")"
R=/workspace/unify/boxbuild/refs
cp -f $R/ActiproSoftware.SyntaxEditor.Net20.dll $R/ActiproSoftware.Shared.Net20.dll $R/ActiproSoftware.WinUICore.Net20.dll . 2>/dev/null || true
/opt/tools/mono/bin/mcs -nowarn:618,169,414,649 -r:System.Windows.Forms -r:System.Drawing -r:ActiproSoftware.SyntaxEditor.Net20.dll -r:ActiproSoftware.Shared.Net20.dll -r:ActiproSoftware.WinUICore.Net20.dll \
  -out:TesteComparador.exe TesteComparador.cs Replicas.cs Preview.cs MotorFalso.cs ../../src/Motor/Comparador.cs ../../src/Nucleo/Tema.cs
xvfb-run -a /opt/tools/mono/bin/mono TesteComparador.exe "${1:-/workspace/unify/previews}"
