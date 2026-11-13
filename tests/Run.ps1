param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$out=Join-Path $repo 'bin/tests'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$dotnet=(Get-Command dotnet).Source
$sdk=& $dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^([^ ]+) \[(.+)\]'){throw '.NET SDK required'}
$csc=Join-Path $Matches[2] ($Matches[1]+'/Roslyn/bincore/csc.dll')
$ref=Get-ChildItem (Join-Path (Split-Path $dotnet) 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '6.*' | Sort-Object Name | Select-Object -Last 1
if(!$ref){throw '.NET 6 reference pack required'}
$refs=@(Get-ChildItem (Join-Path $ref.FullName 'ref/net6.0/*.dll')|ForEach-Object {'-r:'+$_.FullName})
Copy-Item (Join-Path $GameDir 'BepInEx/core/0Harmony.dll') $out
Get-ChildItem (Join-Path $GameDir 'BepInEx/core/Mono*.dll')|Copy-Item -Destination $out
[IO.File]::WriteAllText("$out/SwitchTests.runtimeconfig.json",'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}')
& $dotnet $csc -nologo -optimize- -target:exe "-out:$out/SwitchTests.dll" "-r:$out/0Harmony.dll" $refs "$PSScriptRoot/SwitchTests.cs" "$repo/src/SwitchState.cs" "$repo/src/OwnedPatches.cs"
if($LASTEXITCODE){throw 'Test build failed'}
& $dotnet "$out/SwitchTests.dll"
if($LASTEXITCODE){throw 'Lifecycle tests failed'}
[IO.File]::WriteAllText("$out/DodgeQA.runtimeconfig.json",'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}')
& $dotnet $csc -nologo -optimize+ -target:exe "-out:$out/DodgeQA.dll" $refs "$PSScriptRoot/DodgeQA.cs" "$repo/dodge/DodgeLimit.cs" "$repo/dodge/DodgeRules.cs"
if($LASTEXITCODE){throw 'Dodge tests build failed'}
& $dotnet "$out/DodgeQA.dll"
if($LASTEXITCODE){throw 'Dodge tests failed'}
& "$PSScriptRoot/Installer.Tests.ps1"
