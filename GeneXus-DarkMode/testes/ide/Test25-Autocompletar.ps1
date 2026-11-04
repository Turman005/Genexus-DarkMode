param([string]$Dll = 'GxDarkTheme.dll')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$gx = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$out = Join-Path $here 't25.txt'
"inicio" | Out-File $out -Encoding utf8
$env:GXDARK_SETTINGS = Join-Path $here 't25.settings'
"mode=dark`r`ntema=meianoite`r`nmigrado_xpz=teste" | Out-File $env:GXDARK_SETTINGS -Encoding ascii
$env:GXDARK_LIGHT_ISL = Join-Path $gx 'GenexusOffice2010Silver.isl'
$env:GXDARK_NOMENU = '1'
try {
  Add-Type -AssemblyName System.Windows.Forms, System.Drawing
  Add-Type -ReferencedAssemblies @('System.Windows.Forms', 'System.Drawing') -Path (Join-Path $here 'Test25.cs')
  [T25]::Gx = $gx; [T25]::Out = $out; [T25]::Dir = $here; [T25]::Dll = $Dll; [T25]::Hook()
  [T25]::Run()
} catch { ('ERRO: ' + $_.Exception.ToString()) | Out-File $out -Append }
