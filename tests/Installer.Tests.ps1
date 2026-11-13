param([string]$GameDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$root=Join-Path ([IO.Path]::GetTempPath()) ('ParryInstallerTest-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$package=Join-Path $root 'package';New-Item -ItemType Directory $package | Out-Null
foreach($n in 'Setup.ps1','GamePath.ps1','manifest.json','dist'){Copy-Item -LiteralPath (Join-Path $repo $n) -Destination $package -Recurse}
$game=Join-Path $root 'game with spaces'
foreach($d in 'BepInEx/core','BepInEx/plugins/Other','BepInEx/config','ThreeKingdom_Data/il2cpp_data/Metadata'){New-Item -ItemType Directory -Path (Join-Path $game $d) -Force | Out-Null}
foreach($n in 'ThreeKingdom.exe','winhttp.dll','BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/core/0Harmony.dll'){[IO.File]::WriteAllText((Join-Path $game $n),'fixture')}
[IO.File]::WriteAllText((Join-Path $game 'BepInEx/plugins/Other/Other.dll'),'unrelated-preserve')
[IO.File]::WriteAllText((Join-Path $game 'BepInEx/config/local.heroesvow.counters.cfg'),'unchanged-user-settings')
$meta=Join-Path $game 'ThreeKingdom_Data/il2cpp_data/Metadata/global-metadata.dat'
[IO.File]::WriteAllText($meta,'fixture-metadata')
$m=Get-Content "$package/manifest.json" -Raw | ConvertFrom-Json
$m.gameMetadataSha256=(Get-FileHash $meta).Hash
$m|ConvertTo-Json -Depth 6|Set-Content "$package/manifest.json" -Encoding UTF8
# Isolate process enumeration from a user's running game. Real setup retains its process check.
function Get-Process { @() }
$count=0
function Assert($ok,$message){if(!$ok){throw $message};$script:count++}
function Run($action){& "$package/Setup.ps1" -GameDir $game -Action $action | Out-Null}
function MustFail($action){$failed=$false;try{Run $action}catch{$failed=$true};Assert $failed 'Expected installer rejection'}
$dest=Join-Path $game 'BepInEx/plugins/DuelCounters/DuelCounters.dll'
Run Install
Assert ((Get-FileHash $dest).Hash -eq $m.dllSha256) 'Fresh install hash mismatch'
Assert ((Get-Content "$game/BepInEx/config/local.heroesvow.counters.cfg" -Raw) -eq 'unchanged-user-settings') 'Settings changed'
Run Install
Assert (@(Get-ChildItem "$game/BepInEx/plugins" -Recurse -Filter DuelCounters.dll).Count -eq 1) 'Duplicate DLL'
Run Remove
Assert (!(Test-Path $dest)) 'Remove failed'
Assert ((Get-Content "$game/BepInEx/plugins/Other/Other.dll" -Raw) -eq 'unrelated-preserve') 'Unrelated plugin changed'
Run Remove
[IO.File]::WriteAllText($dest,'unknown-parry')
MustFail Install
Assert ((Get-Content $dest -Raw) -eq 'unknown-parry') 'Unknown plugin overwritten'
Remove-Item -LiteralPath $dest
$wrong=$m.gameMetadataSha256;$m.gameMetadataSha256='0'*64
$m|ConvertTo-Json -Depth 6|Set-Content "$package/manifest.json" -Encoding UTF8
MustFail Install
Assert (!(Test-Path $dest)) 'Version mismatch wrote files'
$m.gameMetadataSha256=$wrong;$m|ConvertTo-Json -Depth 6|Set-Content "$package/manifest.json" -Encoding UTF8
Run Install
$rename=Join-Path $game 'BepInEx/plugins/Other/renamed-parry.dll';Move-Item $dest $rename
Run Install
Assert (!(Test-Path $rename)) 'Renamed duplicate not migrated'
Assert ((Get-FileHash $dest).Hash -eq $m.dllSha256) 'Migration hash mismatch'
# Simulate a copy failure after backup, require exact rollback.
function Copy-Item { throw 'Injected copy failure' }
MustFail Install
Assert ((Get-FileHash $dest).Hash -eq $m.dllSha256) 'Rollback lost original DLL'
Remove-Item Function:\Copy-Item
Run Remove
Assert ((Get-Content "$game/BepInEx/config/local.heroesvow.counters.cfg" -Raw) -eq 'unchanged-user-settings') 'Remove changed settings'
"PASS: $count installer assertions; fixtures: $root"
