param([string]$SourceDir='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
if(!$SourceDir){$SourceDir=Join-Path $repo 'src'}
$out=Join-Path $repo 'bin/card-display-tests'
New-Item -ItemType Directory -Force $out|Out-Null
$refs=@(Get-ChildItem 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/6.0.16/ref/net6.0/*.dll'|ForEach-Object {'-r:'+$_.FullName})
dotnet 'C:/Program Files/dotnet/sdk/7.0.203/Roslyn/bincore/csc.dll' -nologo -target:exe "-out:$out/CardDisplayTests.dll" $refs "$PSScriptRoot/CardDisplayTests.cs" "$SourceDir/DiscardDisplay.cs"
if($LASTEXITCODE){throw 'Test build failed'}
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}'|Set-Content "$out/CardDisplayTests.runtimeconfig.json"
dotnet "$out/CardDisplayTests.dll"
if($LASTEXITCODE){throw 'Card display tests failed'}
