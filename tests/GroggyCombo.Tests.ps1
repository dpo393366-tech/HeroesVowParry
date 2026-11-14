param([string]$SourceDir='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
if(!$SourceDir){$SourceDir=Join-Path $repo 'src'}
$out=Join-Path $repo ('bin/combo-tests-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Force $out|Out-Null
$source=Get-Content (Join-Path $SourceDir 'PerHitParry.cs') -Raw
$start=$source.IndexOf('    internal static bool GroggyCombo(');$end=$source.IndexOf('    static void Analyse(Plan p)',$start)
if($start -lt 0 -or $end -le $start){throw 'Production classifier not found'}
[IO.File]::WriteAllText("$out/Classifier.cs",'using System; namespace Vow.Counters { internal static partial class PerHitParry {'+$source.Substring($start,$end-$start)+'}}')
$refs=@(Get-ChildItem 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.16/ref/net6.0/*.dll'|ForEach-Object {'-r:'+$_.FullName})
dotnet 'C:/Program Files/dotnet/sdk/7.0.203/Roslyn/bincore/csc.dll' -nologo -target:exe "-out:$out/GroggyComboTests.dll" $refs "$PSScriptRoot/GroggyComboTests.cs" "$out/Classifier.cs" "$SourceDir/GroggyMeter.cs" "$SourceDir/GroggyBarMath.cs" "$SourceDir/StrikeLedger.cs"
if($LASTEXITCODE){throw 'Test build failed'}
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}'|Set-Content "$out/GroggyComboTests.runtimeconfig.json"
dotnet "$out/GroggyComboTests.dll"
if($LASTEXITCODE){throw 'Groggy tests failed'}
