# Shared by the standalone BAT, full-package installer and log collector.
# Discovery is read-only and restricted to Steam metadata and nearby folders.
function ConvertTo-HVGameFolder([string]$Value){
    if([string]::IsNullOrWhiteSpace($Value)){return $null}
    try{
        $path=[IO.Path]::GetFullPath($Value.Trim().Trim('"'))
        if([IO.Path]::GetFileName($path) -ieq 'ThreeKingdom.exe'){$path=[IO.Path]::GetDirectoryName($path)}
        if([IO.File]::Exists((Join-Path $path 'ThreeKingdom.exe'))){return $path.TrimEnd('\')}
    }catch{}
    return $null
}
function Read-HVRegistry($Hive,[string]$Key,[string]$Name){
    $handle=$null
    try{$handle=$Hive.OpenSubKey($Key);if($handle){return [string]$handle.GetValue($Name)}}catch{}
    finally{if($handle){$handle.Dispose()}}
}
function Get-HVSteamRoots {
    foreach($hive in @([Microsoft.Win32.Registry]::CurrentUser,[Microsoft.Win32.Registry]::LocalMachine)){
        foreach($key in @('Software\Valve\Steam','Software\WOW6432Node\Valve\Steam')){
            foreach($name in @('SteamPath','InstallPath')){Read-HVRegistry $hive $key $name}
        }
    }
}
function Get-HeroesVowInstallations([string[]]$SteamRoots,[string[]]$NearbyPaths=@(),[switch]$SkipRegistry){
    $found=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $libraries=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    if(!$PSBoundParameters.ContainsKey('SteamRoots')){$SteamRoots=@(Get-HVSteamRoots)}
    foreach($root in $SteamRoots){
        if([string]::IsNullOrWhiteSpace($root)){continue}
        try{$root=[IO.Path]::GetFullPath($root);[void]$libraries.Add($root)}catch{continue}
        foreach($relative in @('steamapps\libraryfolders.vdf','config\libraryfolders.vdf')){
            $file=Join-Path $root $relative
            if(![IO.File]::Exists($file)){continue}
            try{
                $content=[IO.File]::ReadAllText($file)
                foreach($m in [regex]::Matches($content,'(?m)^\s*"(?:path|[0-9]+)"\s+"([^"\r\n]+)"')){
                    $value=$m.Groups[1].Value.Replace('\\','\')
                    if([IO.Path]::IsPathRooted($value)){[void]$libraries.Add([IO.Path]::GetFullPath($value))}
                }
            }catch{}
        }
    }
    $candidates=New-Object 'System.Collections.Generic.List[string]'
    foreach($p in $NearbyPaths){if($p){$candidates.Add($p)}}
    if(!$SkipRegistry){
        foreach($hive in @([Microsoft.Win32.Registry]::CurrentUser,[Microsoft.Win32.Registry]::LocalMachine)){
            foreach($key in @('Software\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3020510','Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3020510')){
                $p=Read-HVRegistry $hive $key 'InstallLocation';if($p){$candidates.Add($p)}
            }
        }
    }
    foreach($lib in $libraries){
        $manifest=Join-Path $lib 'steamapps\appmanifest_3020510.acf'
        if([IO.File]::Exists($manifest)){
            try{
                $text=[IO.File]::ReadAllText($manifest)
                $match=[regex]::Match($text,'"installdir"\s+"([^"\r\n]+)"')
                if($match.Success){$candidates.Add((Join-Path (Join-Path $lib 'steamapps\common') $match.Groups[1].Value))}
            }catch{}
        }
        $candidates.Add((Join-Path $lib 'steamapps\common\LegendOfHeros'))
    }
    foreach($p in $candidates){$valid=ConvertTo-HVGameFolder $p;if($valid){[void]$found.Add($valid)}}
    @($found | Sort-Object)
}
function Select-HeroesVowExecutable {
    $dialog=$null
    try{
        Add-Type -AssemblyName System.Windows.Forms
        $dialog=New-Object Windows.Forms.OpenFileDialog
        $dialog.Title='입지전이 설치된 폴더에서 ThreeKingdom.exe를 선택하세요'
        $dialog.Filter='입지전 실행 파일 (ThreeKingdom.exe)|ThreeKingdom.exe'
        $dialog.CheckFileExists=$true
        if($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK){return $dialog.FileName}
    }catch{Write-Host '찾기 창을 열 수 없습니다. 폴더 경로를 직접 입력하세요.'}
    finally{if($dialog){$dialog.Dispose()}}
}
function Resolve-HeroesVowGameDir([string]$GameDir='', [string[]]$NearbyPaths=@()){
    if(![string]::IsNullOrWhiteSpace($GameDir)){
        $chosen=ConvertTo-HVGameFolder $GameDir
        if(!$chosen){throw ('지정한 경로에 ThreeKingdom.exe가 없습니다: '+$GameDir)}
        Write-Host ('대상 게임 폴더: '+$chosen)
        return $chosen
    }
    $found=@(Get-HeroesVowInstallations -NearbyPaths $NearbyPaths)
    Write-Host '입지전 설치 위치를 선택하세요.'
    if($found.Count){for($i=0;$i -lt $found.Count;$i++){Write-Host (('{0}. {1}' -f ($i+1),$found[$i]))}}
    else{Write-Host 'Steam에서 설치 위치를 찾지 못했습니다.'}
    while($true){
        $prompt='번호 또는 게임 폴더 경로 입력 / B = 찾아보기 / Q = 취소'
        if($found.Count -eq 1){$prompt+=' / Enter = 위 경로'}
        $answer=Read-Host $prompt
        if($null -eq $answer){throw '설치 경로 선택이 취소되었습니다.'}
        $answer=$answer.Trim()
        if($answer -ieq 'Q'){throw '설치 경로 선택이 취소되었습니다.'}
        if($answer -ieq 'B'){$answer=Select-HeroesVowExecutable;if(!$answer){continue}}
        if(!$answer -and $found.Count -eq 1){$answer=$found[0]}
        $index=0
        if([int]::TryParse($answer,[ref]$index) -and $index -ge 1 -and $index -le $found.Count){$answer=$found[$index-1]}
        $chosen=ConvertTo-HVGameFolder $answer
        if($chosen){Write-Host ('대상 게임 폴더: '+$chosen);return $chosen}
        Write-Host '그 위치에 ThreeKingdom.exe가 없습니다. 다시 선택하세요.'
    }
}
