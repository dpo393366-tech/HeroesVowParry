param([string]$GameDir='',[string]$OutputDir='')
$ErrorActionPreference='Stop'
if(!$GameDir){throw '게임 폴더 경로를 GameDir로 지정하세요.'}
if(!$OutputDir){$OutputDir=Join-Path $PSScriptRoot 'dist'}
$game=[IO.Path]::GetFullPath($GameDir)
$dotnet=(Get-Command dotnet).Source
$sdk=& $dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK가 필요합니다.'}
$csc=Join-Path $Matches[2] ($Matches[1]+'\Roslyn\bincore\csc.dll')
$packs=Join-Path (Split-Path $dotnet) 'packs\Microsoft.NETCore.App.Ref'
$ref=Get-ChildItem $packs -Directory | Where-Object Name -Like '6.*' | Sort-Object Name | Select-Object -Last 1
if(!$ref){throw '.NET 6 reference pack이 필요합니다.'}
$refs=@(Get-ChildItem (Join-Path $ref.FullName 'ref\net6.0\*.dll') | ForEach-Object {'-r:'+$_.FullName})
foreach($n in 'Assembly-CSharp','Assembly-CSharp-firstpass','DOTween','UnityEngine.IMGUIModule','UnityEngine.UIModule','UnityEngine.TextRenderingModule','UnityEngine.CoreModule','UnityEngine.AnimationModule','UnityEngine.AssetBundleModule','UnityEngine.AIModule','UnityEngine.AudioModule','UnityEngine.ParticleSystemModule','Il2Cppmscorlib','UnityEngine.UI','UnityEngine.InputLegacyModule','UnityEngine.PhysicsModule','UnityEngine.SharedInternalsModule'){
 $file=Join-Path $game "BepInEx\interop\$n.dll";if(Test-Path $file){$refs+='-r:'+$file}
}
foreach($n in 'BepInEx.Core','BepInEx.Unity.IL2CPP','0Harmony','Il2CppInterop.Runtime'){$refs+='-r:'+(Join-Path $game "BepInEx\core\$n.dll")}
$resources=@(Get-ChildItem "$PSScriptRoot\assets\*.pcm" | ForEach-Object {'-resource:'+$_.FullName+','+$_.Name})
$sources=@(Get-ChildItem "$PSScriptRoot\src\*.cs" | ForEach-Object {$_.FullName})
New-Item -ItemType Directory -Force -Path $OutputDir|Out-Null
& $dotnet $csc -nologo -deterministic -optimize+ -target:library -define:DuelCounters "-out:$OutputDir\DuelCounters.dll" $refs $resources $sources
if($LASTEXITCODE){throw '패링 모드 빌드 실패'}
& $dotnet $csc -nologo -deterministic -optimize+ -target:library "-out:$OutputDir\DodgeLimit.dll" $refs "$PSScriptRoot\dodge\DodgeLimit.cs" "$PSScriptRoot\dodge\DodgeRules.cs"
if($LASTEXITCODE){throw '회피 제한 모드 빌드 실패'}
Get-FileHash "$OutputDir\DuelCounters.dll" -Algorithm SHA256
Get-FileHash "$OutputDir\DodgeLimit.dll" -Algorithm SHA256
