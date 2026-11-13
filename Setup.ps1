param([string]$GameDir='', [ValidateSet('Install','Remove')][string]$Action='Install')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePath.ps1')
$GameDir=Resolve-HeroesVowGameDir -GameDir $GameDir -NearbyPaths @($PSScriptRoot,(Split-Path $PSScriptRoot))
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
function Hash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
foreach($p in Get-Process ThreeKingdom -ErrorAction SilentlyContinue){
    try{$running=$p.Path}catch{throw '실행 중인 입지전을 종료한 뒤 설치하세요.'}
    if(!$running -or [IO.Path]::GetDirectoryName($running).TrimEnd('\') -ieq $GameDir.TrimEnd('\')){throw '대상 입지전을 종료한 뒤 설치하세요.'}
}
$pluginRoot=Join-Path $GameDir 'BepInEx\plugins'
$dest=Join-Path $pluginRoot 'DuelCounters\DuelCounters.dll'
$payload=Join-Path $PSScriptRoot 'dist\DuelCounters.dll'
if($Action -eq 'Install'){
    foreach($n in 'BepInEx\core\BepInEx.Unity.IL2CPP.dll','BepInEx\core\0Harmony.dll','winhttp.dll'){
        if(!(Test-Path -LiteralPath (Join-Path $GameDir $n))){throw 'BepInEx IL2CPP 로더를 먼저 설치하세요. 게임 본체와 로더는 이 패키지에 포함되지 않습니다.'}
    }
    $meta=Join-Path $GameDir 'ThreeKingdom_Data\il2cpp_data\Metadata\global-metadata.dat'
    if(!(Test-Path -LiteralPath $meta) -or (Hash $meta) -ne $manifest.gameMetadataSha256){throw '검증한 게임 데이터 버전과 다릅니다. 설치하지 않았습니다.'}
    if((Hash $payload) -ne $manifest.dllSha256){throw '패키지 DLL 무결성 확인에 실패했습니다.'}
}
$existing=@()
if(Test-Path -LiteralPath $pluginRoot){
    $links=@(Get-ChildItem -LiteralPath $pluginRoot -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
    if($links.Count){throw 'plugins 안에 연결 폴더가 있어 자동 교체를 중단했습니다. 연결 구조를 확인하세요.'}
    # Assembly identity also catches renamed copies of the old parry plugin.
    foreach($f in Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter '*.dll'){
        $identity=''
        try{$identity=[Reflection.AssemblyName]::GetAssemblyName($f.FullName).Name}catch{}
        if($f.Name -ieq 'DuelCounters.dll' -or $identity -eq 'DuelCounters'){$existing+= $f}
    }
}
$known=@($manifest.knownParrySha256)+@($manifest.dllSha256)
foreach($f in $existing){
    $h=Hash $f.FullName
    if($h -notin $known){throw ('확인되지 않은 패링 DLL이 있습니다. 덮어쓰지 않았습니다: '+$f.FullName)}
    if($Action -eq 'Remove' -and $h -ne $manifest.dllSha256){throw '이 설치기로 설치한 독립 패링 DLL만 제거할 수 있습니다.'}
}
if($Action -eq 'Remove' -and !$existing.Count){Write-Host '독립 패링 모드가 설치되어 있지 않습니다.';return}
$backup=Join-Path $GameDir ('BepInEx\disabled-mods\ParryBackup-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$moved=@();$newWritten=$false
try{
    foreach($f in $existing){
        $relative=$f.FullName.Substring($pluginRoot.Length).TrimStart('\')
        $saved=Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
        Move-Item -LiteralPath $f.FullName -Destination $saved
        $moved+=@{Original=$f.FullName;Backup=$saved}
    }
    if($Action -eq 'Install'){
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        $newWritten=$true
        Copy-Item -LiteralPath $payload -Destination $dest
        if((Hash $dest) -ne $manifest.dllSha256){throw '설치 후 파일 검증 실패'}
    }
    @($moved)|ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'backup-index.json') -Encoding UTF8
}catch{
    if($newWritten -and (Test-Path -LiteralPath $dest)){Remove-Item -LiteralPath $dest}
    foreach($record in $moved){Move-Item -LiteralPath $record.Backup -Destination $record.Original}
    throw
}
Write-Host ('완료: '+$Action)
Write-Host ('기존 DLL 백업: '+$backup)
Write-Host '기존 설정은 유지됩니다. 게임의 MOD 매니저에서 무투·설전 패링을 켜고 끌 수 있습니다.'
Write-Host 'F11: 패링 세부 설정. 전투 중 ON/OFF 변경은 전투 종료 후 적용됩니다.'
