param([string]$SourceDir='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
if(!$SourceDir){$SourceDir=Join-Path $repo 'src'}
$out=Join-Path $repo 'bin/event-tests'
New-Item -ItemType Directory -Force $out|Out-Null
$refs=@(Get-ChildItem 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.16/ref/net6.0/*.dll'|ForEach-Object {'-r:'+$_.FullName})
dotnet 'C:/Program Files/dotnet/sdk/7.0.203/Roslyn/bincore/csc.dll' -nologo -target:exe "-out:$out/EventBattleTests.dll" $refs "$PSScriptRoot/EventBattleTests.cs" "$SourceDir/PlayerBattleRules.cs" "$SourceDir/GuardInfoInput.cs" "$SourceDir/MartialTurnFrequency.cs"
if($LASTEXITCODE){throw 'Test build failed'}
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}'|Set-Content "$out/EventBattleTests.runtimeconfig.json"
dotnet "$out/EventBattleTests.dll"
if($LASTEXITCODE){throw 'Event battle tests failed'}
foreach($file in 'DuelCounters.cs','RealtimeParry.cs','LateParry.cs','EnemyGroggy.cs','StrengthPressure.cs'){
 $text=Get-Content -LiteralPath (Join-Path $SourceDir $file) -Raw
 if($text.Contains('PlayerBattleRules.Participating(')){throw "Protagonist-only parry gate remains in $file"}
 if(!$text.Contains('PlayerBattleRules.ManualCombat(')){throw "Event combat gate missing in $file"}
}
'PASS: parry input, late settlement, groggy and strength use event-combat gates.'
