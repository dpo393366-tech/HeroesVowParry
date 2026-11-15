$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$out=Join-Path $repo 'bin/draw-tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$dotnet=(Get-Command dotnet).Source
$sdk=& $dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw 'SDK required'}
$csc=Join-Path $Matches[2] ($Matches[1]+'/Roslyn/bincore/csc.dll')
$ref=Get-ChildItem (Join-Path (Split-Path $dotnet) 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '6.*' | Sort-Object Name | Select-Object -Last 1
$refs=@(Get-ChildItem (Join-Path $ref.FullName 'ref/net6.0/*.dll') | ForEach-Object {'-r:'+$_.FullName})
& $dotnet $csc -nologo -target:exe "-out:$out/DrawRestoreTests.dll" $refs "$PSScriptRoot/DrawRestoreTests.cs" "$repo/src/DiscardDisplay.cs"
if($LASTEXITCODE){throw 'Draw restoration test build failed'}
[IO.File]::WriteAllText("$out/DrawRestoreTests.runtimeconfig.json",'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}')
& $dotnet "$out/DrawRestoreTests.dll"
if($LASTEXITCODE){throw 'Draw restoration tests failed'}
