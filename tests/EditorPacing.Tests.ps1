param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$out=Join-Path $repo 'bin/editor-pacing-tests'
New-Item -ItemType Directory -Force $out | Out-Null
$dotnet=(Get-Command dotnet).Source
$sdk=& $dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK required'}
$csc=Join-Path $Matches[2] ($Matches[1]+'/Roslyn/bincore/csc.dll')
$ref=Get-ChildItem (Join-Path (Split-Path $dotnet) 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '6.*' | Sort-Object Name | Select-Object -Last 1
$refs=@(Get-ChildItem (Join-Path $ref.FullName 'ref/net6.0/*.dll')|ForEach-Object {'-r:'+$_.FullName})
Copy-Item (Join-Path $GameDir 'BepInEx/core/0Harmony.dll') $out
Get-ChildItem (Join-Path $GameDir 'BepInEx/core/Mono*.dll') | Copy-Item -Destination $out
& $dotnet $csc -nologo -nowarn:0649 -target:exe "-out:$out/EditorPacing.dll" "-r:$out/0Harmony.dll" $refs "$PSScriptRoot/EditorPacing/PacingStubs.cs" "$PSScriptRoot/EditorPacing/PacingComparison.cs" "$PSScriptRoot/EditorPacing/BaselineAttackPacing.cs" "$repo/src/AttackPacing.cs" "$repo/src/OwnedPatches.cs"
if($LASTEXITCODE){throw 'Editor pacing test compilation failed'}
[IO.File]::WriteAllText("$out/EditorPacing.runtimeconfig.json",'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}')
& $dotnet "$out/EditorPacing.dll"
if($LASTEXITCODE){throw 'Editor pacing tests failed'}
