param([string]$GameDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$root=Join-Path ([IO.Path]::GetTempPath()) ('ParryInstaller21-'+[Guid]::NewGuid().ToString('N'))
$package=Join-Path $root 'package';New-Item -ItemType Directory -Path $package -Force|Out-Null
foreach($n in 'Setup.ps1','GamePath.ps1','Loader.ps1','manifest.json','dist'){Copy-Item -LiteralPath (Join-Path $repo $n) -Destination $package -Recurse}
$game=Join-Path $root 'game with spaces'
foreach($d in 'BepInEx/core','BepInEx/plugins/Other','BepInEx/config','ThreeKingdom_Data/il2cpp_data/Metadata'){New-Item -ItemType Directory -Path (Join-Path $game $d) -Force|Out-Null}
foreach($n in 'ThreeKingdom.exe','winhttp.dll','doorstop_config.ini','BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/core/0Harmony.dll','BepInEx/core/Il2CppInterop.Runtime.dll'){[IO.File]::WriteAllText((Join-Path $game $n),'other-version-preserve')}
[IO.File]::WriteAllText((Join-Path $game 'BepInEx/plugins/Other/Other.dll'),'unrelated-preserve')
foreach($n in 'local.heroesvow.counters.cfg','local.heroesvow.dodgelimit.cfg','BepInEx.cfg'){[IO.File]::WriteAllText((Join-Path $game ('BepInEx/config/'+$n)),'user-settings')}
[IO.File]::WriteAllText((Join-Path $game 'ThreeKingdom_Data/il2cpp_data/Metadata/global-metadata.dat'),'different-game-update')
$m=Get-Content "$package/manifest.json" -Raw|ConvertFrom-Json
function Get-Process { @() }
$count=0
function Assert($ok,$message){if(!$ok){throw $message};$script:count++}
function Run($action){& "$package/Setup.ps1" -GameDir $game -Action $action|Out-Null}
function MustFail($action){$failed=$false;try{Run $action}catch{$failed=$true};Assert $failed 'Expected failure'}
$dest=Join-Path $game 'BepInEx/plugins/DuelCounters/DuelCounters.dll'
$dodge=Join-Path $game 'BepInEx/plugins/DodgeLimit/DodgeLimit.dll'
Run Install
Assert (Test-Path "$game/BepInEx/patchers/HeroesVowParry/ParryAutoUpdate.dll") 'Updater not installed'
Assert (Test-Path "$game/BepInEx/patchers/HeroesVowParry/owner.json") 'Owner record missing'
foreach($module in $m.modules){Assert ((Get-FileHash (Join-Path $game ('BepInEx/plugins/'+$module.name+'/'+$module.name+'.dll'))).Hash -eq $module.sha256) 'Module copy failed'}
Assert ((Get-Content "$game/winhttp.dll" -Raw) -eq 'other-version-preserve') 'Existing injector changed'
Assert ((Get-Content "$game/BepInEx/core/0Harmony.dll" -Raw) -eq 'other-version-preserve') 'Existing runtime changed'
Run Install
Assert (@(Get-ChildItem "$game/BepInEx/plugins" -Recurse -Filter DuelCounters.dll).Count -eq 1) 'Duplicate parry'
Assert (@(Get-ChildItem "$game/BepInEx/plugins" -Recurse -Filter DodgeLimit.dll).Count -eq 1) 'Duplicate dodge'
foreach($n in 'local.heroesvow.counters.cfg','local.heroesvow.dodgelimit.cfg','BepInEx.cfg'){Assert ((Get-Content (Join-Path $game ('BepInEx/config/'+$n)) -Raw) -eq 'user-settings') 'Settings changed'}
Run Remove
Assert (!(Test-Path "$game/BepInEx/patchers/HeroesVowParry/ParryAutoUpdate.dll")) 'Updater survived removal'
Assert (!(Test-Path "$game/BepInEx/patchers/HeroesVowParry/owner.json")) 'Owner survived removal'
Assert (!(Test-Path $dest) -and !(Test-Path $dodge)) 'Remove failed'
Assert ((Get-Content "$game/BepInEx/plugins/Other/Other.dll" -Raw) -eq 'unrelated-preserve') 'Other mod changed'
Run Remove
[IO.File]::WriteAllText($dest,'unknown-custom-parry-version')
[IO.File]::WriteAllText($dodge,'unknown-custom-dodge-version')
Run Install
Assert ((Get-FileHash $dest).Hash -eq $m.modules[0].sha256) 'Unlisted parry version rejected'
Assert ((Get-FileHash $dodge).Hash -eq $m.modules[1].sha256) 'Unlisted dodge version rejected'
$backups=@(Get-ChildItem "$game/BepInEx/disabled-mods" -Recurse -Filter '*.dll')
Assert (@($backups|Where-Object {([IO.File]::ReadAllText($_.FullName)) -eq 'unknown-custom-parry-version'}).Count -eq 1) 'Unlisted previous version not backed up'
$rename=Join-Path $game 'BepInEx/plugins/Other/renamed-parry.dll';Move-Item $dest $rename
$renameDodge=Join-Path $game 'BepInEx/plugins/Other/renamed-dodge.dll';Move-Item $dodge $renameDodge
Run Install
Assert (!(Test-Path $rename) -and !(Test-Path $renameDodge)) 'Renamed module duplicate'
$script:copies=0
function Copy-Item {param($LiteralPath,$Destination) $script:copies++;if($script:copies -eq 2){throw 'Injected second copy failure'};Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination}
MustFail Install
Assert ((Get-FileHash $dest).Hash -eq $m.modules[0].sha256) 'Parry rollback failed'
Assert ((Get-FileHash $dodge).Hash -eq $m.modules[1].sha256) 'Dodge rollback failed'
Remove-Item Function:\Copy-Item
# Missing loader: exact official archive, no existing runtime replacement.
$game=Join-Path $root 'fresh game';New-Item -ItemType Directory $game|Out-Null
[IO.File]::WriteAllText("$game/ThreeKingdom.exe",'fixture')
[IO.File]::WriteAllText("$game/changelog.txt",'game-notes-preserve')
Run Install
Assert (Test-Path "$game/BepInEx/core/BepInEx.Unity.IL2CPP.dll") 'Fresh loader missing'
Assert (Test-Path "$game/dotnet/coreclr.dll") 'Fresh runtime missing'
Assert (Test-Path "$game/winhttp.dll") 'Fresh injector missing'
Assert ((Get-Content "$game/changelog.txt" -Raw) -eq 'game-notes-preserve') 'Root file overwritten'
# A distinct injector must not be overwritten, but plugin installation is allowed.
$game=Join-Path $root 'other loader';New-Item -ItemType Directory $game|Out-Null
[IO.File]::WriteAllText("$game/ThreeKingdom.exe",'fixture');[IO.File]::WriteAllText("$game/winhttp.dll",'third-party-injector')
Run Install
Assert ((Get-Content "$game/winhttp.dll" -Raw) -eq 'third-party-injector') 'Other injector overwritten'
Assert (Test-Path "$game/BepInEx/plugins/DuelCounters/DuelCounters.dll") 'Different environment installation blocked'
# Integrity remains separate from environment compatibility.
[IO.File]::WriteAllText("$package/dist/DuelCounters.dll",'corrupted-payload')
MustFail Install
"PASS: $count installer assertions; fixtures: $root"
