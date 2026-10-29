<#
  Compila a GxDarkTheme.dll e o Setup-GxDarkTheme.exe no Windows.
  - Usa o .NET SDK (dotnet) se estiver instalado; senao usa o csc.exe que ja vem com o .NET Framework 4.8.
  - As referencias vem da propria instalacao do GeneXus 18 (nada e copiado para o repositorio).
  - Le a versao de compatibilidade (packages.<N>.xml em %ProgramData%\GeneXus\GeneXus\18) e grava em src\Nucleo\Compat.cs.
  Nao instala nada e nao mexe no GeneXus: so gera os arquivos em dist\.

  Uso:  powershell -ExecutionPolicy Bypass -File scripts\Build.ps1 [-GeneXus "C:\Program Files (x86)\GeneXus\GeneXus18"] [-Compat 143920] [-Saida dist] [-SemInstalador] [-UsarCsc]
#>
param(
  [string]$GeneXus,
  [int]$Compat = 0,
  [string]$Saida,
  [switch]$SemInstalador,
  [switch]$UsarCsc
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
if (-not $Saida) { $Saida = Join-Path $raiz 'dist' }
New-Item -ItemType Directory -Force -Path $Saida | Out-Null

if (-not $GeneXus) {
  $cands = foreach ($pf in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
    if ($pf -and (Test-Path (Join-Path $pf 'GeneXus'))) { Get-ChildItem (Join-Path $pf 'GeneXus') -Directory -Filter 'GeneXus18*' | Where-Object { Test-Path (Join-Path $_.FullName 'GeneXus.exe') } }
  }
  $GeneXus = @($cands | Sort-Object Name -Descending | Select-Object -First 1).FullName
  if (-not $GeneXus) { throw 'GeneXus 18 nao encontrado; informe -GeneXus <pasta>' }
}
Write-Host "GeneXus de referencia: $GeneXus"

# versao de compatibilidade
if ($Compat -le 0) {
  $catDir = Join-Path $env:ProgramData 'GeneXus\GeneXus\18'
  $nums = @(Get-ChildItem $catDir -Filter 'packages.*.xml' -ErrorAction SilentlyContinue | ForEach-Object { if ($_.Name -match '^packages\.(\d+)\.xml$') { [int]$Matches[1] } } | Sort-Object -Descending)
  if ($nums.Count -gt 0) { $Compat = $nums[0] } else { $Compat = 143920 }
}
$compatCs = Join-Path $raiz 'src\Nucleo\Compat.cs'
$txt = [IO.File]::ReadAllText($compatCs)
$novo = [regex]::Replace($txt, 'public const int Versao = \d+;', "public const int Versao = $Compat;")
if ($novo -ne $txt) { [IO.File]::WriteAllText($compatCs, $novo, (New-Object Text.UTF8Encoding($true))); Write-Host "Compat.cs atualizado para $Compat" } else { Write-Host "Compatibilidade: $Compat" }

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet -and -not $UsarCsc) {
  & dotnet build (Join-Path $raiz 'src\GxDarkTheme.csproj') -c Release "-p:GeneXusDir=$GeneXus" -nologo -v q
  if ($LASTEXITCODE -ne 0) { throw 'falha ao compilar a DLL' }
  if (-not $SemInstalador) { & dotnet build (Join-Path $raiz 'installer\Setup.csproj') -c Release -nologo -v q; if ($LASTEXITCODE -ne 0) { throw 'falha ao compilar o instalador' } }
  $bin = Join-Path $raiz 'bin'
} else {
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
  $bin = Join-Path $env:TEMP ('gxdarktheme-build-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
  New-Item -ItemType Directory -Force -Path $bin | Out-Null
  try {
    $refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xml.dll', 'System.Design.dll')
    foreach ($pat in @('Artech.*.dll', 'DevExpress.*v7.3.dll', 'Infragistics2.Win.v11.1.dll', 'Infragistics2.Shared.v11.1.dll', 'Infragistics2.Win.UltraWinToolbars.v11.1.dll', 'ActiproSoftware.SyntaxEditor.Net20.dll', 'ActiproSoftware.Shared.Net20.dll', 'ActiproSoftware.WinUICore.Net20.dll')) {
      $refs += @(Get-ChildItem $GeneXus -Filter $pat -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    }
    $rsp = Join-Path $bin 'dll.rsp'
    $l = New-Object Collections.Generic.List[string]
    $l.Add('/nologo'); $l.Add('/target:library'); $l.Add('/optimize+'); $l.Add('/langversion:5'); $l.Add('/nowarn:1701,1702,618,1591')
    $l.Add('/out:"' + (Join-Path $bin 'GxDarkTheme.dll') + '"')
    foreach ($r in $refs) { $l.Add('/reference:"' + $r + '"') }
    Get-ChildItem (Join-Path $raiz 'resources\icons') -File | ForEach-Object { $l.Add('/resource:"' + $_.FullName + '",GxIcons.' + $_.Name) }
    $l.Add('/resource:"' + (Join-Path $raiz 'resources\roles.txt') + '",GxDarkTheme.roles.txt')
    $l.Add('/resource:"' + (Join-Path $raiz 'LICENSE') + '",GxDarkTheme.LICENSE.txt')
    Get-ChildItem (Join-Path $raiz 'src') -Recurse -Filter *.cs | ForEach-Object { $l.Add('"' + $_.FullName + '"') }
    [IO.File]::WriteAllLines($rsp, $l)
    & $csc "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw 'csc: falha ao compilar a DLL' }
    if (-not $SemInstalador) {
      $l = New-Object Collections.Generic.List[string]
      $l.Add('/nologo'); $l.Add('/target:winexe'); $l.Add('/optimize+'); $l.Add('/langversion:5')
      $l.Add('/out:"' + (Join-Path $bin 'Setup-GxDarkTheme.exe') + '"')
      $l.Add('/win32manifest:"' + (Join-Path $raiz 'installer\app.manifest') + '"')
      $l.Add('/win32icon:"' + (Join-Path $raiz 'installer\setup.ico') + '"')
      foreach ($r in @('System.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xml.dll')) { $l.Add('/reference:' + $r) }
      $l.Add('/resource:"' + (Join-Path $bin 'GxDarkTheme.dll') + '",payload.GxDarkTheme.dll')
      $l.Add('/resource:"' + (Join-Path $raiz 'LICENSE') + '",payload.LICENSE.txt')
      $l.Add('/resource:"' + (Join-Path $raiz 'THIRD-PARTY-NOTICES.md') + '",payload.THIRD-PARTY-NOTICES.md')
      $l.Add('/resource:"' + (Join-Path $raiz 'resources\icons\NOTICE.txt') + '",payload.NOTICE-Mercury.txt')
      $l.Add('/resource:"' + (Join-Path $raiz 'LEIA-ME.md') + '",payload.LEIA-ME.md')
      foreach ($f in @('installer\Setup.cs', 'installer\Operacoes.cs', 'src\Nucleo\Produto.cs', 'src\Nucleo\Compat.cs', 'src\Nucleo\Tema.cs')) { $l.Add('"' + (Join-Path $raiz $f) + '"') }
      $rsp2 = Join-Path $bin 'setup.rsp'
      [IO.File]::WriteAllLines($rsp2, $l)
      & $csc "@$rsp2"
      if ($LASTEXITCODE -ne 0) { throw 'csc: falha ao compilar o instalador' }
    }
  } catch { throw }
}
Copy-Item (Join-Path $bin 'GxDarkTheme.dll') $Saida -Force
if (-not $SemInstalador) { Copy-Item (Join-Path $bin 'Setup-GxDarkTheme.exe') $Saida -Force }
if ($bin -like "$env:TEMP*") { Remove-Item $bin -Recurse -Force }
Get-ChildItem $Saida -File -Include *.dll, *.exe -Recurse | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } | Tee-Object (Join-Path $Saida 'SHA256SUMS.txt')
Write-Host "Pronto: $Saida"
