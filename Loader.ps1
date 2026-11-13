# Reuse existing runtimes; a clean install receives the official x64 IL2CPP archive.
function Get-HVLoaderPlan([string]$GameDir,$Manifest,[string]$PackageRoot,[string]$Staging){
    $modern=Join-Path $GameDir 'BepInEx/core/BepInEx.Unity.IL2CPP.dll'
    if(Test-Path -LiteralPath $modern){
        Write-Host '기존 BepInEx IL2CPP를 재사용합니다. 로더 버전·설정·다른 플러그인은 유지합니다.'
        foreach($n in 'BepInEx/core/0Harmony.dll','BepInEx/core/Il2CppInterop.Runtime.dll','winhttp.dll','doorstop_config.ini'){
            if(!(Test-Path -LiteralPath (Join-Path $GameDir $n))){Write-Warning ('기존 로더에서 파일을 찾지 못했습니다: '+$n+' · 다른 로더 연결 방식을 사용한다면 그대로 유지됩니다.')}
        }
        return
    }
    $occupied=@('BepInEx/core','winhttp.dll','doorstop_config.ini','dotnet/coreclr.dll') | Where-Object {Test-Path -LiteralPath (Join-Path $GameDir $_)}
    if($occupied.Count){
        Write-Warning '기존의 다른/불완전한 로더 구성이 있습니다. 모드 파일은 설치하되 로더를 덮어쓰지 않습니다. 실행에 필요한 BepInEx 6 IL2CPP 연결은 별도로 확인하세요.'
        return
    }
    $archive=Join-Path $PackageRoot $Manifest.loader.file
    if(!(Test-Path -LiteralPath $archive)){throw '자동 설치용 Loader ZIP이 없습니다. 배포 ZIP 전체를 압축 해제하세요.'}
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $Manifest.loader.sha256){throw 'Loader ZIP이 손상되었습니다. 배포 파일을 다시 받으세요.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try{
        $base=[IO.Path]::GetFullPath($Staging).TrimEnd('\')+'\'
        foreach($entry in $zip.Entries){
            if(!$entry.Name){continue}
            $relative=$entry.FullName.Replace('/','\')
            $path=[IO.Path]::GetFullPath((Join-Path $base $relative))
            if(!$path.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw '올바르지 않은 Loader ZIP 경로입니다.'}
            New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
            $target=Join-Path $GameDir $relative
            if(!(Test-Path -LiteralPath $target)){[pscustomobject]@{Source=$path;Destination=$target}}
        }
    }finally{$zip.Dispose()}
    Write-Host 'BepInEx가 없어 동봉한 공식 IL2CPP 로더를 자동 설치합니다. 최초 실행은 준비 시간이 더 필요할 수 있습니다.'
}
