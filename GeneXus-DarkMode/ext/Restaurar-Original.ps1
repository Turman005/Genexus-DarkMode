# Funcao comum (Ativar e Desativar): desfaz o que a versao ANTIGA do Ativar gravava fora da extensao.
# So devolve ao original o que ainda estiver exatamente com o valor escuro que o Ativar antigo gravou;
# qualquer coisa que voce mudou depois (outra cor, outro tema) fica como esta.
function Restore-GxOriginal {
    param([string]$BackupDir, [switch]$DryRun)
    $cfgDir = Join-Path $env:APPDATA 'GeneXus\GeneXus\18'
    $cfg    = Join-Path $cfgDir 'TextEditorConfig_Default.xml'
    $bkCfg  = Join-Path $BackupDir 'TextEditorConfig_Default.xml'
    $bkReg  = Join-Path $BackupDir 'SystemColors.txt'
    $bkDwm  = Join-Path $BackupDir 'WindowsTheme.txt'
    $did = 0

    # 1) Cores do editor: troca so o bloco <HighlightingStyles> pelo do backup (fonte, tabulacao etc. ficam como estao)
    if ((Test-Path $cfg) -and (Test-Path $bkCfg)) {
        $cur = Get-Content $cfg -Raw
        if ($cur.Contains('<ForeColor>-11100970</ForeColor>') -or $cur.Contains('Key="DefaultStyle"')) {
            [xml]$x = $cur
            [xml]$b = Get-Content $bkCfg -Raw
            $curHs = $x.SelectSingleNode('/TextEditorConfig/HighlightingStyles')
            $bkHs  = $b.SelectSingleNode('/TextEditorConfig/HighlightingStyles')
            if ($curHs -and $bkHs -and $DryRun) { Write-Host '[simulacao] restauraria as cores do editor'; $did++ }
            elseif ($curHs -and $bkHs) {
                $copy = Join-Path $BackupDir ('TextEditorConfig_Default.escuro-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.xml')
                Copy-Item $cfg $copy -Force
                [void]$x.DocumentElement.ReplaceChild($x.ImportNode($bkHs, $true), $curHs)
                $x.Save($cfg)
                Write-Host 'Cores originais do editor restauradas (TextEditorConfig_Default.xml).' -ForegroundColor Green
                $did++
            }
        }
    }

    # 2) Cores de sistema do Windows: so as que ainda estao com o valor escuro do Ativar antigo
    $dark = @{ 'Scrollbar'='36 45 60'; 'ActiveTitle'='36 45 60'; 'InactiveTitle'='24 31 42'; 'Menu'='24 31 42'; 'Window'='17 21 28'; 'WindowFrame'='51 61 71'
               'MenuText'='240 244 250'; 'WindowText'='220 226 235'; 'TitleText'='240 244 250'; 'ActiveBorder'='36 45 60'; 'InactiveBorder'='24 31 42'
               'AppWorkspace'='17 21 28'; 'Hilight'='38 79 120'; 'HilightText'='255 255 255'; 'ButtonFace'='24 31 42'; 'ButtonShadow'='12 15 20'
               'GrayText'='110 122 138'; 'ButtonText'='240 244 250'; 'InactiveTitleText'='138 144 155'; 'ButtonHilight'='61 73 87'; 'ButtonDkShadow'='8 10 14'
               'ButtonLight'='51 61 71'; 'InfoText'='220 226 235'; 'InfoWindow'='36 45 60'; 'ButtonAlternateFace'='31 39 52'; 'HotTrackingColor'='91 167 255'
               'GradientActiveTitle'='36 45 60'; 'GradientInactiveTitle'='24 31 42'; 'MenuHilight'='31 78 121'; 'MenuBar'='36 45 60' }
    $map = @{ 'Scrollbar'=0; 'ActiveTitle'=2; 'InactiveTitle'=3; 'Menu'=4; 'Window'=5; 'WindowFrame'=6; 'MenuText'=7; 'WindowText'=8
              'TitleText'=9; 'ActiveBorder'=10; 'InactiveBorder'=11; 'AppWorkspace'=12; 'Hilight'=13; 'HilightText'=14; 'ButtonFace'=15
              'ButtonShadow'=16; 'GrayText'=17; 'ButtonText'=18; 'InactiveTitleText'=19; 'ButtonHilight'=20; 'ButtonDkShadow'=21
              'ButtonLight'=22; 'InfoText'=23; 'InfoWindow'=24; 'ButtonAlternateFace'=25; 'HotTrackingColor'=26
              'GradientActiveTitle'=27; 'GradientInactiveTitle'=28; 'MenuHilight'=29; 'MenuBar'=30 }
    if (Test-Path $bkReg) {
        $colorsKey = Get-ItemProperty 'HKCU:\Control Panel\Colors'
        $idx = @(); $val = @()
        foreach ($line in Get-Content $bkReg) {
            $n, $v = $line -split '=', 2
            if (-not $map.ContainsKey($n)) { continue }
            if ([string]$colorsKey.$n -ne $dark[$n]) { continue }
            if ([string]$colorsKey.$n -eq $v) { continue }
            if ($DryRun) { Write-Host ('[simulacao] ' + $n + ': ' + $colorsKey.$n + ' -> ' + $v); $idx += $map[$n]; continue }
            Set-ItemProperty 'HKCU:\Control Panel\Colors' -Name $n -Value $v
            $p = $v -split ' '
            $idx += $map[$n]
            $val += ([int]$p[0]) + ([int]$p[1] -shl 8) + ([int]$p[2] -shl 16)
        }
        if ($idx.Count -gt 0 -and $DryRun) { $did++ }
        elseif ($idx.Count -gt 0) {
            if (-not ('GxDark.Native' -as [type])) { Add-Type -Namespace GxDark -Name Native -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetSysColors(int c, int[] e, int[] v);' }
            [GxDark.Native]::SetSysColors($idx.Count, $idx, $val) | Out-Null
            Write-Host ('Cores originais do Windows restauradas (' + $idx.Count + ' itens).') -ForegroundColor Green
            $did++
        }
    }

    # 3) Tema de apps / cor de destaque da barra de titulo: so o que ainda estiver com o valor do Ativar antigo
    $ours = @{ 'AppsUseLightTheme'=0; 'ColorPrevalence'=1; 'AccentColor'=-12833500; 'AccentColorInactive'=-14016744 }
    if (Test-Path $bkDwm) {
        $changed = 0
        foreach ($line in Get-Content $bkDwm) {
            $path, $name, $v = $line -split '\|', 3
            $now = (Get-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue).$name
            # o registro pode devolver o DWORD com ou sem sinal: compara os 32 bits
            if ($null -eq $now -or -not $ours.ContainsKey($name)) { continue }
            if (([int64]$now -band [int64]4294967295) -ne ([int64]$ours[$name] -band [int64]4294967295)) { continue }
            if ($DryRun) { Write-Host ('[simulacao] ' + $name + ': ' + $now + ' -> ' + $v); $changed++; continue }
            if ($v -eq 'ABSENT') { Remove-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue }
            else { Set-ItemProperty -Path $path -Name $name -Value ([BitConverter]::ToInt32([BitConverter]::GetBytes([int64]$v), 0)) -Type DWord -ErrorAction SilentlyContinue }
            $changed++
        }
        if ($changed -gt 0 -and $DryRun) { $did++ }
        elseif ($changed -gt 0) {
            if (-not ('GxDark.Broadcast' -as [type])) { Add-Type -Namespace GxDark -Name Broadcast -MemberDefinition '[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);' }
            $r = [UIntPtr]::Zero
            [void][GxDark.Broadcast]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'ImmersiveColorSet', 2, 3000, [ref]$r)
            Write-Host 'Tema claro dos apps e barra de titulo originais restaurados (se algo continuar escuro, saia e entre no Windows).' -ForegroundColor Green
            $did++
        }
    }
    return $did
}
