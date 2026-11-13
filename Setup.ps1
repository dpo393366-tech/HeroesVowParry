param([string]$GameDir='', [ValidateSet('Install','Remove')][string]$Action='Install')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePath.ps1')
. (Join-Path $PSScriptRoot 'Loader.ps1')
$GameDir=Resolve-HeroesVowGameDir -GameDir $GameDir -NearbyPaths @($PSScriptRoot,(Split-Path $PSScriptRoot))
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
function Hash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
$unknownProcess=$false
foreach($p in Get-Process ThreeKingdom -ErrorAction SilentlyContinue){
    if($p.HasExited){continue}
    try{$running=$p.Path}catch{$running=$null}
    if(!$running){$unknownProcess=$true;continue}
    if([IO.Path]::GetDirectoryName($running).TrimEnd('\') -ieq $GameDir.TrimEnd('\')){throw '대상 입지전을 종료한 뒤 설치하세요.'}
}
if($unknownProcess){Write-Warning '경로를 읽을 수 없는 동명 프로세스가 있습니다. 실제 대상 게임이 실행 중이면 종료 후 설치하세요.'}
$pluginRoot=Join-Path $GameDir 'BepInEx\plugins'
$moduleNames=@('DuelCounters','DodgeLimit')
$existing=@()
function Get-HVPlugins([string]$Folder){
    if(!(Test-Path -LiteralPath $Folder)){return}
    foreach($item in Get-ChildItem -LiteralPath $Folder -Force){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){Write-Warning ('연결 항목은 보존합니다: '+$item.FullName);continue}
        if($item.PSIsContainer){Get-HVPlugins $item.FullName}
        elseif($item.Extension -ieq '.dll'){$item}
    }
}
foreach($f in Get-HVPlugins $pluginRoot){
    $identity='';try{$identity=[Reflection.AssemblyName]::GetAssemblyName($f.FullName).Name}catch{}
    if($f.BaseName -in $moduleNames -or $identity -in $moduleNames){$existing+=$f}
}
if($Action -eq 'Remove' -and !$existing.Count){Write-Host '패링·회피 제한 DLL이 설치되어 있지 않습니다.';return}
$plan=@();$stage=Join-Path ([IO.Path]::GetTempPath()) ('HeroesVowParry-'+[Guid]::NewGuid().ToString('N'))
if($Action -eq 'Install'){
    # Version and existing DLL hash equality are not installation gates.
    # Only package integrity is checked, never the user's game/mod equality.
    foreach($module in $manifest.modules){
        $source=Join-Path $PSScriptRoot $module.file
        if((Hash $source) -ne $module.sha256){throw ('배포 파일이 손상되었습니다: '+$module.file)}
        $plan+= [pscustomobject]@{Source=$source;Destination=(Join-Path $pluginRoot ($module.name+'\'+$module.name+'.dll'))}
    }
    $plan+=@(Get-HVLoaderPlan $GameDir $manifest $PSScriptRoot $stage)
}
$backup=Join-Path $GameDir ('BepInEx\disabled-mods\ParryBackup-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$moved=@();$written=@()
try{
    foreach($f in $existing){
        $relative=$f.FullName.Substring($pluginRoot.Length).TrimStart('\')
        $saved=Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
        Move-Item -LiteralPath $f.FullName -Destination $saved
        $moved+=@{Original=$f.FullName;Backup=$saved}
    }
    foreach($entry in $plan){
        New-Item -ItemType Directory -Path (Split-Path $entry.Destination) -Force | Out-Null
        $written+=$entry.Destination
        Copy-Item -LiteralPath $entry.Source -Destination $entry.Destination
        if((Hash $entry.Destination) -ne (Hash $entry.Source)){throw '설치 후 복사 검증 실패'}
    }
    @($moved)|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $backup 'backup-index.json') -Encoding UTF8
}catch{
    foreach($path in $written){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    foreach($record in $moved){Move-Item -LiteralPath $record.Backup -Destination $record.Original}
    throw
}
Write-Host ('완료: '+$Action+' · 패링 + 회피 제한')
Write-Host ('이전 파일 백업: '+$backup)
Write-Host '설정·기존 로더·다른 모드는 유지됩니다. 게임 재실행 후 MOD 매니저에서 패링 ON/OFF, F11에서 패링·회피 제한을 조정하세요.'
