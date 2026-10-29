param([string]$Dll = 'GxDarkTheme.dll')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$gx = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$out = Join-Path $here 't24.txt'
"inicio" | Out-File $out -Encoding utf8
$env:GXDARK_SETTINGS = Join-Path $here 't24.settings'
"mode=dark" | Out-File $env:GXDARK_SETTINGS -Encoding ascii
$env:GXDARK_LIGHT_ISL = Join-Path $gx 'GenexusOffice2010Silver.isl'
try {
  Add-Type -AssemblyName System.Windows.Forms, System.Drawing
  $refs = @('System.Windows.Forms', 'System.Drawing', 'System.Xml', "$gx\Infragistics2.Win.v11.1.dll", "$gx\Infragistics2.Shared.v11.1.dll", "$gx\Infragistics2.Win.UltraWinToolbars.v11.1.dll", "$gx\Infragistics2.Win.UltraWinTree.v11.1.dll",
    "$gx\DevExpress.XtraTreeList.v7.3.dll", "$gx\DevExpress.Utils.v7.3.dll", "$gx\DevExpress.Data.v7.3.dll", "$gx\DevExpress.XtraEditors.v7.3.dll", "$gx\Artech.FrameworkDE.dll", "$gx\MagicLocalLibrary.dll", "$gx\ActiproSoftware.SyntaxEditor.Net20.dll", "$gx\ActiproSoftware.Shared.Net20.dll", "$gx\ActiproSoftware.WinUICore.Net20.dll")
  $refs = @($refs | Where-Object { ($_ -notlike '*.dll') -or (Test-Path $_) })
  foreach ($r in $refs) { if ($r -like '*.dll') { [void][Reflection.Assembly]::LoadFrom($r) } }
  Add-Type -ReferencedAssemblies $refs -Path (Join-Path $here 'Test24.cs')
  [T24]::Gx = $gx; [T24]::Out = $out; [T24]::Dir = $here; [T24]::Dll = $Dll; [T24]::Hook()
  [T24]::Run()
} catch { ('ERRO: ' + $_.Exception.ToString()) | Out-File $out -Append }
